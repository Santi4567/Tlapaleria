using Api_Tlapaleria.DTOs;

namespace Api_Tlapaleria.Services
{
    public interface ICreditAccountService
    {
        Task<CreditPortfolioSummaryDto> GetPortfolioSummaryAsync();

        Task<PagedResponse<CreditAccountDetailDto>> GetAccountsAsync(
            int? customerId = null,
            string? status = null,
            bool onlyOverdue = false,
            string? search = null,
            int pageNumber = 1,
            int pageSize = 20);

        Task<CreditAccountDetailDto> GetAccountByIdAsync(int accountId);
        Task<CreditAccountDetailDto?> GetAccountBySaleIdAsync(int saleId);
        Task<CreditAccountDetailDto> RegisterAccountPaymentAsync(int accountId, RegisterCreditPaymentDto dto, int userId);
        Task<GeneralCustomerPaymentResultDto> RegisterGeneralCustomerPaymentAsync(int customerId, RegisterCreditPaymentDto dto, int userId);
        Task<CreditAccountDetailDto> UpdateAccountMetadataAsync(int accountId, UpdateCreditAccountDto dto);

        Task<PagedResponse<CreditPaymentDto>> GetPaymentsHistoryAsync(
            int? customerId = null,
            int? accountId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int pageNumber = 1,
            int pageSize = 30);
    }
}
