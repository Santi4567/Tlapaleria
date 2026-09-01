using Api_Tlapaleria.DTOs;

namespace Api_Tlapaleria.Services
{
    public interface IReportService
    {
        // -- Reporte de Vnetas Netas y brutas --
        Task<FinancialReportDto> GetFinancialReportAsync(DateTime? startDate, DateTime? endDate);

        // --- Reporte de precios de un producto ---
        Task<ProductPriceHistoryDto> GetPriceHistoryAsync(int productId);
    }
}