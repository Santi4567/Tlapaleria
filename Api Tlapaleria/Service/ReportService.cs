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

            // 3. Rango de fechas: default de 12 meses si no se especifica, con tope máximo de 2 años
            var startDateEfectivo = startDate ?? DateTime.Now.AddYears(-1);
            var endDateEfectivo = endDate.HasValue ? endDate.Value.Date.AddDays(1).AddTicks(-1) : (DateTime?)null;

            if (endDateEfectivo.HasValue)
            {
                var rangoSolicitado = endDateEfectivo.Value - startDateEfectivo;
                if (rangoSolicitado.TotalDays > 730)
                    throw new Exception("El rango de fechas solicitado no puede exceder 2 años. Divide la consulta en periodos más cortos.");
            }

            // 4. Traemos TODO el historial (precio y costo) de las presentaciones filtradas, sin recortar aún por fecha —
            //    lo necesitamos completo para poder calcular el "valor inicial" antes del rango
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

            // 5. Semilla: calculamos el estado ACUMULADO justo antes del rango pedido
            var ultimoPrecioPorPresentacion = idsPresentaciones.ToDictionary(id => id, id => (decimal?)null);
            var ultimoCostoPorPresentacion = idsPresentaciones.ToDictionary(id => id, id => (decimal?)null);

            var eventosAntesDelRango = timelineCompleto.Where(e => e.CreatedAt < startDateEfectivo);

            foreach (var evento in eventosAntesDelRango)
            {
                if (evento.EsCosto)
                    ultimoCostoPorPresentacion[evento.PresentationId] = evento.Valor;
                else
                    ultimoPrecioPorPresentacion[evento.PresentationId] = evento.Valor;
            }

            // 6. Filtramos ahora sí el timeline al rango pedido
            var timelineEnRango = timelineCompleto.Where(e =>
                e.CreatedAt >= startDateEfectivo &&
                (!endDateEfectivo.HasValue || e.CreatedAt <= endDateEfectivo.Value)
            ).ToList();

            var filas = new List<PriceHistoryRowDto>();

            // 7. Fila "semilla" en startDateEfectivo mostrando el estado heredado, aunque no haya cambio exacto ese día
            filas.Add(new PriceHistoryRowDto
            {
                Date = startDateEfectivo,
                PresentationPrices = new Dictionary<int, decimal?>(ultimoPrecioPorPresentacion),
                PresentationSupplierPrices = new Dictionary<int, decimal?>(ultimoCostoPorPresentacion)
            });

            // 8. Forward-fill normal, solo con los eventos dentro del rango
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

            return new ProductPriceHistoryDto
            {
                ProductId = producto.Id,
                ProductName = producto.Name,
                Presentations = producto.Presentations
                    .Where(p => idsPresentaciones.Contains(p.Id))
                    .Select(p => new PresentationInfoDto { PresentationId = p.Id, Name = p.Name })
                    .ToList(),
                History = filas
            };
        }
    }
}