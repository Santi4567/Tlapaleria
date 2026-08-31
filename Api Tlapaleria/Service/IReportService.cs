using Api_Tlapaleria.DTOs;

namespace Api_Tlapaleria.Services
{
    public interface IReportService
    {
        Task<FinancialReportDto> GetFinancialReportAsync(DateTime? startDate, DateTime? endDate);
    }
}