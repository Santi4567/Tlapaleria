using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Enums;

namespace Api_Tlapaleria.Services
{
    public interface IReportService
    {
        // -- Reporte de Ventas Netas y brutas --
        Task<FinancialReportDto> GetFinancialReportAsync(
            DateTime? startDate,
            DateTime? endDate,
            FinancialReportPeriod periodType = FinancialReportPeriod.CustomRange);

        // --- Reporte de precios de un producto (o de una sola presentación, con rango de fechas) ---
        Task<ProductPriceHistoryDto> GetPriceHistoryAsync(int productId, int? presentationId = null, DateTime? startDate = null, DateTime? endDate = null);

        // --- Reporte de ventas y separación de dinero por proveedor ---
        Task<SupplierSalesReportResponseDto> GetSupplierSalesReportAsync(DateTime? startDate = null, DateTime? endDate = null, SupplierReportPeriod periodType = SupplierReportPeriod.AllTime);
    }
}