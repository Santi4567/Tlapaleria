using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Enums;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class ReportService : IReportService
    {
        private readonly TlapaleriaContext _context;

        public ReportService(TlapaleriaContext context)
        {
            _context = context;
        }

        public async Task<FinancialReportDto> GetFinancialReportAsync(
            DateTime? startDate,
            DateTime? endDate,
            FinancialReportPeriod periodType = FinancialReportPeriod.CustomRange)
        {
            var now = DateTime.Now;
            DateTime effectiveStart;
            DateTime effectiveEnd;

            // 1. Determinar el rango de fechas según el botón seleccionado (periodType)
            switch (periodType)
            {
                case FinancialReportPeriod.Day: // 1: HOY (o el día enviado en startDate)
                    var targetDay = startDate?.Date ?? now.Date;
                    effectiveStart = targetDay;
                    effectiveEnd = targetDay.AddDays(1).AddTicks(-1);
                    break;

                case FinancialReportPeriod.Month: // 2: MES (actual o el mes de startDate)
                    var targetMonth = startDate ?? now;
                    effectiveStart = new DateTime(targetMonth.Year, targetMonth.Month, 1);
                    effectiveEnd = effectiveStart.AddMonths(1).AddTicks(-1);
                    break;

                case FinancialReportPeriod.Year: // 3: AÑO (actual o el año de startDate)
                    var targetYear = startDate?.Year ?? now.Year;
                    effectiveStart = new DateTime(targetYear, 1, 1);
                    effectiveEnd = new DateTime(targetYear, 12, 31, 23, 59, 59, 999);
                    break;

                case FinancialReportPeriod.CustomRange: // 0: Rango libre (con protección de 2 años)
                default:
                    if (!startDate.HasValue && !endDate.HasValue)
                    {
                        effectiveStart = now.Date.AddYears(-1);
                        effectiveEnd = now.Date.AddDays(1).AddTicks(-1);
                    }
                    else
                    {
                        effectiveStart = startDate?.Date ?? now.Date.AddYears(-1);
                        var rawEnd = endDate?.Date ?? now.Date;

                        if (effectiveStart > rawEnd)
                            (effectiveStart, rawEnd) = (rawEnd, effectiveStart);

                        effectiveEnd = rawEnd.AddDays(1).AddTicks(-1);

                        if ((effectiveEnd - effectiveStart).TotalDays > 731)
                            throw new Exception("El rango de fechas no puede exceder los 2 años.");
                    }
                    break;
            }

            // 2. Consultas base aplicando las reglas de negocio (IsActive) y el rango de fechas
            var salesQuery = _context.Sales
                .Where(s => s.IsActive && s.CreatedAt >= effectiveStart && s.CreatedAt <= effectiveEnd)
                .AsQueryable();

            var returnsQuery = _context.Returns
                .Where(r => r.Sale.IsActive && r.CreatedAt >= effectiveStart && r.CreatedAt <= effectiveEnd)
                .AsQueryable();

            // 3. Obtener Totales Generales (Resumen)
            var totalSalesCount = await salesQuery.CountAsync();
            var grossAmount = await salesQuery.SumAsync(s => (decimal?)s.TotalAmount) ?? 0m;
            var returnsAmount = await returnsQuery.SumAsync(r => (decimal?)r.TotalRefunded) ?? 0m;

            // Costo total de lo vendido (todas las líneas de las ventas del rango)
            var costOfGoodsSold = await salesQuery
                .SelectMany(s => s.Details)
                .SumAsync(d => (decimal?)d.SupplierCostSubtotal) ?? 0m;

            // Costo de lo devuelto (usando el ancla SaleDetailId)
            var costOfReturnedGoods = await _context.ReturnDetails
                .Where(rd => rd.Return!.Sale!.IsActive && returnsQuery.Select(r => r.Id).Contains(rd.ReturnId))
                .SumAsync(rd => (decimal?)(rd.QuantityReturned * rd.SaleDetail!.SupplierPriceAtSale)) ?? 0m;

            var netCost = costOfGoodsSold - costOfReturnedGoods;
            var realProfit = (grossAmount - returnsAmount) - netCost;

            // 4. Extraer eventos con su timestamp exacto para agrupar según el periodo
            var rawSales = await salesQuery
                .Select(s => new
                {
                    s.CreatedAt,
                    s.TotalAmount,
                    Cost = s.Details.Sum(d => (decimal?)d.SupplierCostSubtotal) ?? 0m
                })
                .ToListAsync();

            var rawReturns = await returnsQuery
                .Select(r => new
                {
                    r.CreatedAt,
                    r.TotalRefunded,
                    ReturnedCost = r.Details.Sum(rd => (decimal?)(rd.QuantityReturned * rd.SaleDetail!.SupplierPriceAtSale)) ?? 0m
                })
                .ToListAsync();

            // 5. Agrupar y formatear etiquetas según el periodo (Hora, Semana, Mes o Día)
            string[] mesesAbrev = { "", "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };

            (int SortKey, string Label) GetBucket(DateTime dt)
            {
                switch (periodType)
                {
                    case FinancialReportPeriod.Day:
                        // Solo en HOY aparece la hora: ej. "09:00", "14:00"
                        return (dt.Hour, $"{dt.Hour:D2}:00");

                    case FinancialReportPeriod.Month:
                        // Semanas con su rango exacto de días: ej. "Sem 1 (01-07)"
                        int weekNum = (dt.Day - 1) / 7 + 1;
                        int startDay = (weekNum - 1) * 7 + 1;
                        int daysInMonth = DateTime.DaysInMonth(dt.Year, dt.Month);
                        int endDay = Math.Min(weekNum * 7, daysInMonth);
                        return (weekNum, $"Sem {weekNum} ({startDay:D2}-{endDay:D2})");

                    case FinancialReportPeriod.Year:
                        // Por mes del año con nombre corto: ej. "Ene 2026", "Feb 2026"
                        return (dt.Month, $"{mesesAbrev[dt.Month]} {dt.Year}");

                    default:
                        // Rango personalizado por día: ej. "2026-10-13"
                        return (int.Parse(dt.ToString("yyyyMMdd")), dt.ToString("yyyy-MM-dd"));
                }
            }

            var salesGrouped = rawSales
                .GroupBy(s => GetBucket(s.CreatedAt))
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Count = g.Count(),
                        Gross = g.Sum(x => x.TotalAmount),
                        TotalCost = g.Sum(x => x.Cost)
                    });

            var returnsGrouped = rawReturns
                .GroupBy(r => GetBucket(r.CreatedAt))
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Refunded = g.Sum(x => x.TotalRefunded),
                        ReturnedCost = g.Sum(x => x.ReturnedCost)
                    });

            var allBuckets = salesGrouped.Keys
                .Union(returnsGrouped.Keys)
                .OrderBy(k => k.SortKey)
                .ToList();

            var chartData = new List<ChartDataPointDto>();

            foreach (var bucket in allBuckets)
            {
                salesGrouped.TryGetValue(bucket, out var saleData);
                returnsGrouped.TryGetValue(bucket, out var returnData);

                var count = saleData?.Count ?? 0;
                var gross = saleData?.Gross ?? 0m;
                var cost = saleData?.TotalCost ?? 0m;

                var refund = returnData?.Refunded ?? 0m;
                var returnedCost = returnData?.ReturnedCost ?? 0m;

                var netSales = gross - refund;
                var bucketNetCost = cost - returnedCost;
                var bucketRealProfit = netSales - bucketNetCost;

                chartData.Add(new ChartDataPointDto
                {
                    DateLabel = bucket.Label,
                    SalesCount = count,
                    NetAmount = netSales,
                    RealProfitAmount = bucketRealProfit
                });
            }

            // 6. Retornar el objeto final ensamblado
            return new FinancialReportDto
            {
                TotalSalesCount = totalSalesCount,
                GrossSalesAmount = grossAmount,
                TotalRefundedAmount = returnsAmount,
                NetSalesAmount = grossAmount - returnsAmount,
                CostOfGoodsSold = netCost,
                RealProfitAmount = realProfit,
                ChartData = chartData
            };
        }

        // -- Reporte de precios para los productos (con filtro opcional por presentación y rango de fechas)
        public async Task<ProductPriceHistoryDto> GetPriceHistoryAsync(int productId, int? presentationId = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            // 1. Validar que el producto exista, con sus presentaciones
            var producto = await _context.Products
                .Include(p => p.Presentations)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (producto == null)
                throw new Exception($"El producto con ID {productId} no fue encontrado.");

            if (!producto.Presentations.Any())
                throw new Exception("Este producto no tiene presentaciones registradas.");

            // 2. Filtrar a una sola presentación si se pidió, o usar todas
            var idsPresentaciones = presentationId.HasValue
                ? producto.Presentations.Where(p => p.Id == presentationId.Value).Select(p => p.Id).ToList()
                : producto.Presentations.Select(p => p.Id).ToList();

            if (presentationId.HasValue && !idsPresentaciones.Any())
                throw new Exception($"La presentación con ID {presentationId.Value} no pertenece a este producto.");

            // --- NUEVO: validación de fechas ---
            if (endDate.HasValue && !startDate.HasValue)
                throw new Exception("Si especificas 'endDate', también debes especificar 'startDate'.");

            bool usoRangoExplicito = startDate.HasValue;

            // --- NUEVO: normalización de orden cronológico, sin importar cuál mandaron primero ---
            DateTime rangeStart;
            DateTime? rangeEnd;

            if (usoRangoExplicito)
            {
                if (endDate.HasValue)
                {
                    rangeStart = startDate.Value <= endDate.Value ? startDate.Value : endDate.Value;
                    rangeEnd = startDate.Value <= endDate.Value ? endDate.Value : startDate.Value;
                }
                else
                {
                    rangeStart = startDate.Value;
                    rangeEnd = null; // sin límite superior explícito
                }
            }
            else
            {
                // Caso 1: sin fechas -> default de últimos 12 meses
                rangeStart = DateTime.Now.AddYears(-1);
                rangeEnd = null;
            }

            // Tope de 2 años (usando "ahora" si no hay límite superior explícito)
            var limiteParaValidar = rangeEnd ?? DateTime.Now;
            if ((limiteParaValidar - rangeStart).TotalDays > 730)
                throw new Exception("El rango de fechas solicitado no puede exceder 2 años. Divide la consulta en periodos más cortos.");

            var rangeEndFinDeDia = rangeEnd.HasValue ? rangeEnd.Value.Date.AddDays(1).AddTicks(-1) : (DateTime?)null;

            // 3. Traemos TODO el historial, sin filtrar aún por fecha
            var eventosPrecios = await _context.PresentationPriceHistories
                .Where(h => idsPresentaciones.Contains(h.PresentationId))
                .OrderBy(h => h.CreatedAt)
                .Select(h => new { h.CreatedAt, h.PresentationId, Valor = (decimal?)h.NewPrice })
                .ToListAsync();

            var eventosCosto = await _context.PresentationSupplierPriceHistories
                .Where(h => idsPresentaciones.Contains(h.PresentationId))
                .OrderBy(h => h.CreatedAt)
                .Select(h => new { h.CreatedAt, h.PresentationId, Valor = (decimal?)h.NewSupplierPrice })
                .ToListAsync();

            var timelinePrecios = eventosPrecios.Select(e => (e.CreatedAt, e.PresentationId, EsCosto: false, e.Valor));
            var timelineCostos = eventosCosto.Select(e => (e.CreatedAt, e.PresentationId, EsCosto: true, e.Valor));
            var timelineCompleto = timelinePrecios.Concat(timelineCostos).OrderBy(e => e.CreatedAt).ToList();

            // 4. Semilla: estado acumulado justo antes de rangeStart
            var ultimoPrecioPorPresentacion = idsPresentaciones.ToDictionary(id => id, id => (decimal?)null);
            var ultimoCostoPorPresentacion = idsPresentaciones.ToDictionary(id => id, id => (decimal?)null);

            // Si rangeStart es HOY, usamos la tabla viva como fuente de verdad, no el historial
            bool rangeStartEsHoy = rangeStart.Date == DateTime.Now.Date && !rangeEnd.HasValue;
            if (rangeStartEsHoy)
            {
                foreach (var presId in idsPresentaciones)
                {
                    var presentacionViva = producto.Presentations.First(p => p.Id == presId);
                    ultimoPrecioPorPresentacion[presId] = presentacionViva.Price;
                    ultimoCostoPorPresentacion[presId] = presentacionViva.SupplierPrice;
                }
            }
            else
            {
                foreach (var evento in timelineCompleto.Where(e => e.CreatedAt <= rangeStart))
                {
                    if (evento.EsCosto)
                        ultimoCostoPorPresentacion[evento.PresentationId] = evento.Valor;
                    else
                        ultimoPrecioPorPresentacion[evento.PresentationId] = evento.Valor;
                }
            }

            var filas = new List<PriceHistoryRowDto>();

            bool haySemillaReal = ultimoPrecioPorPresentacion.Values.Any(v => v.HasValue)
                                || ultimoCostoPorPresentacion.Values.Any(v => v.HasValue);

            if (haySemillaReal)
            {
                filas.Add(new PriceHistoryRowDto
                {
                    Date = rangeStart,
                    PresentationPrices = new Dictionary<int, decimal?>(ultimoPrecioPorPresentacion),
                    PresentationSupplierPrices = new Dictionary<int, decimal?>(ultimoCostoPorPresentacion)
                });
            }

            // 5. Eventos estrictamente después de rangeStart, hasta rangeEnd (si hay límite)
            var timelineEnRango = timelineCompleto.Where(e =>
                e.CreatedAt > rangeStart &&
                (!rangeEndFinDeDia.HasValue || e.CreatedAt <= rangeEndFinDeDia.Value)
            ).ToList();

            foreach (var evento in timelineEnRango)
            {
                if (evento.EsCosto)
                    ultimoCostoPorPresentacion[evento.PresentationId] = evento.Valor;
                else
                    ultimoPrecioPorPresentacion[evento.PresentationId] = evento.Valor;

                filas.Add(new PriceHistoryRowDto
                {
                    Date = evento.CreatedAt,
                    PresentationPrices = new Dictionary<int, decimal?>(ultimoPrecioPorPresentacion),
                    PresentationSupplierPrices = new Dictionary<int, decimal?>(ultimoCostoPorPresentacion)
                });
            }

            // 6. NUEVO: fila de "cierre" en rangeEnd, solo si se pidió un endDate explícito
            //    y no hay ya un evento real fechado exactamente ese día
            if (rangeEnd.HasValue)
            {
                bool yaHayFilaEseDia = filas.Any(f => f.Date.Date == rangeEnd.Value.Date);
                if (!yaHayFilaEseDia)
                {
                    filas.Add(new PriceHistoryRowDto
                    {
                        Date = rangeEnd.Value.Date,
                        PresentationPrices = new Dictionary<int, decimal?>(ultimoPrecioPorPresentacion),
                        PresentationSupplierPrices = new Dictionary<int, decimal?>(ultimoCostoPorPresentacion)
                    });
                }
            }

            // 7. Colapsar filas del mismo segundo (evita duplicados por eventos casi simultáneos)
            var filasFinal = filas
                .GroupBy(f => new DateTime(f.Date.Year, f.Date.Month, f.Date.Day, f.Date.Hour, f.Date.Minute, f.Date.Second))
                .Select(g => g.OrderBy(f => f.Date).Last())
                .OrderBy(f => f.Date)
                .ToList();

            // 8. NUEVO: Caso 1 (sin rango explícito) -> agregar/anclar el nodo de "hoy" con actual:true
            if (!usoRangoExplicito)
            {
                var filaDeHoy = filasFinal.FirstOrDefault(f => f.Date.Date == DateTime.Now.Date);

                if (filaDeHoy != null)
                {
                    filaDeHoy.Actual = true;
                }
                else
                {
                    var presentacionesFiltradas = producto.Presentations
                        .Where(p => idsPresentaciones.Contains(p.Id))
                        .ToList();

                    filasFinal.Add(new PriceHistoryRowDto
                    {
                        Date = DateTime.Now,
                        PresentationPrices = presentacionesFiltradas.ToDictionary(p => p.Id, p => (decimal?)p.Price),
                        PresentationSupplierPrices = presentacionesFiltradas.ToDictionary(p => p.Id, p => (decimal?)p.SupplierPrice),
                        Actual = true
                    });
                }
            }

            return new ProductPriceHistoryDto
            {
                ProductId = producto.Id,
                ProductName = producto.Name,
                Presentations = producto.Presentations
                    .Where(p => idsPresentaciones.Contains(p.Id))
                    .Select(p => new PresentationInfoDto { PresentationId = p.Id, Name = p.Name })
                    .ToList(),
                History = filasFinal
            };
        }
    }
}