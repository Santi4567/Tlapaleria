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

        // -- Reporte de precios para los productos 
        // -- Reporte de precios para los productos 
        public async Task<ProductPriceHistoryDto> GetPriceHistoryAsync(int productId)
        {
            // 1. Validar que el producto exista, con sus presentaciones
            var producto = await _context.Products
                .Include(p => p.Presentations)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (producto == null)
                throw new Exception($"El producto con ID {productId} no fue encontrado.");

            if (!producto.Presentations.Any())
                throw new Exception("Este producto no tiene presentaciones registradas.");

            var idsPresentaciones = producto.Presentations.Select(p => p.Id).ToList();

            // 2. Historial de PRECIO de venta, de todas las presentaciones, en una sola consulta
            var eventosPrecios = await _context.PresentationPriceHistories
                .Where(h => idsPresentaciones.Contains(h.PresentationId))
                .OrderBy(h => h.CreatedAt)
                .Select(h => new { h.CreatedAt, h.PresentationId, Valor = (decimal?)h.NewPrice })
                .ToListAsync();

            // 3. Historial de COSTO de proveedor, de todas las presentaciones, en una sola consulta
            var eventosCosto = await _context.PresentationSupplierPriceHistories
                .Where(h => idsPresentaciones.Contains(h.PresentationId))
                .OrderBy(h => h.CreatedAt)
                .Select(h => new { h.CreatedAt, h.PresentationId, Valor = (decimal?)h.NewSupplierPrice })
                .ToListAsync();

            // 4. Unimos ambos historiales en una sola línea de tiempo, ya no hace falta distinguir con PresentationId = null
            //    porque ahora AMBOS eventos (precio y costo) siempre traen su PresentationId
            var timelinePrecios = eventosPrecios.Select(e => (e.CreatedAt, e.PresentationId, EsCosto: false, e.Valor));
            var timelineCostos = eventosCosto.Select(e => (e.CreatedAt, e.PresentationId, EsCosto: true, e.Valor));
            var timeline = timelinePrecios.Concat(timelineCostos).OrderBy(e => e.CreatedAt).ToList();

            // 5. Forward-fill: arrastramos el último valor conocido de PRECIO y de COSTO, por cada presentación
            var ultimoPrecioPorPresentacion = idsPresentaciones.ToDictionary(id => id, id => (decimal?)null);
            var ultimoCostoPorPresentacion = idsPresentaciones.ToDictionary(id => id, id => (decimal?)null);

            var filas = new List<PriceHistoryRowDto>();

            foreach (var evento in timeline)
            {
                if (evento.EsCosto)
                    ultimoCostoPorPresentacion[evento.PresentationId] = evento.Valor;
                else
                    ultimoPrecioPorPresentacion[evento.PresentationId] = evento.Valor;

                filas.Add(new PriceHistoryRowDto
                {
                    Date = evento.CreatedAt,
                    PresentationPrices = new Dictionary<int, decimal?>(ultimoPrecioPorPresentacion), // copia, no referencia
                    PresentationSupplierPrices = new Dictionary<int, decimal?>(ultimoCostoPorPresentacion) // copia, no referencia
                });
            }

            return new ProductPriceHistoryDto
            {
                ProductId = producto.Id,
                ProductName = producto.Name,
                Presentations = producto.Presentations
                    .Select(p => new PresentationInfoDto { PresentationId = p.Id, Name = p.Name })
                    .ToList(),
                History = filas
            };
        }
    }
}