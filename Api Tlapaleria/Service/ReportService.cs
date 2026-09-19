using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
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

        public async Task<FinancialReportDto> GetFinancialReportAsync(DateTime? startDate, DateTime? endDate)
        {
            // 1. Consultas base aplicando las reglas de negocio (IsActive)
            var salesQuery = _context.Sales.Where(s => s.IsActive).AsQueryable();
            // Asumiendo que tu tabla de devoluciones se llama Returns y tiene relación con Sale
            var returnsQuery = _context.Returns.Where(r => r.Sale.IsActive).AsQueryable();

            // 2. Aplicar filtros de fechas si existen
            if (startDate.HasValue)
            {
                salesQuery = salesQuery.Where(s => s.CreatedAt >= startDate.Value);
                returnsQuery = returnsQuery.Where(r => r.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                salesQuery = salesQuery.Where(s => s.CreatedAt <= endOfDay);
                returnsQuery = returnsQuery.Where(r => r.CreatedAt <= endOfDay);
            }

            // 3. Obtener Totales Generales (Resumen)
            var totalSalesCount = await salesQuery.CountAsync();
            var grossAmount = await salesQuery.SumAsync(s => (decimal?)s.TotalAmount) ?? 0m;
            var returnsAmount = await returnsQuery.SumAsync(r => (decimal?)r.TotalRefunded) ?? 0m;

            // --- NUEVO: Costo total de lo vendido (todas las líneas de las ventas del rango) ---
            var costOfGoodsSold = await salesQuery
                .SelectMany(s => s.Details)
                .SumAsync(d => (decimal?)d.SupplierCostSubtotal) ?? 0m;

            // --- NUEVO: Costo de lo devuelto (usando el ancla SaleDetailId) ---
            var costOfReturnedGoods = await _context.ReturnDetails
                .Where(rd => rd.Return!.Sale!.IsActive && returnsQuery.Select(r => r.Id).Contains(rd.ReturnId))
                .SumAsync(rd => (decimal?)(rd.QuantityReturned * rd.SaleDetail!.SupplierPriceAtSale)) ?? 0m;

            var netCost = costOfGoodsSold - costOfReturnedGoods;
            var realProfit = (grossAmount - returnsAmount) - netCost;

            // 4. Obtener datos agrupados por día para la gráfica
            var salesByDate = await salesQuery
                .GroupBy(s => s.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count(),
                    Gross = g.Sum(s => s.TotalAmount)
                })
                .ToListAsync();

            var returnsByDate = await returnsQuery
                .GroupBy(r => r.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Refunded = g.Sum(r => r.TotalRefunded)
                })
                .ToListAsync();

            // 5. Unificar ventas y devoluciones por fecha en memoria
            var allDates = salesByDate.Select(s => s.Date)
                .Union(returnsByDate.Select(r => r.Date))
                .OrderBy(d => d)
                .ToList();

            var chartData = new List<ChartDataPointDto>();

            foreach (var date in allDates)
            {
                var saleData = salesByDate.FirstOrDefault(s => s.Date == date);
                var returnData = returnsByDate.FirstOrDefault(r => r.Date == date);

                var dayCount = saleData?.Count ?? 0;
                var dayGross = saleData?.Gross ?? 0m;
                var dayRefund = returnData?.Refunded ?? 0m;

                chartData.Add(new ChartDataPointDto
                {
                    DateLabel = date.ToString("yyyy-MM-dd"),
                    SalesCount = dayCount,
                    NetAmount = dayGross - dayRefund
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