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
                ChartData = chartData
            };
        }
    }
}