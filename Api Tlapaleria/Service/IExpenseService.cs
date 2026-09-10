using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IExpenseService
    {
        // --- 1. EGRESOS (Pagos y compras al contado) ---
        Task<Expense> CreateExpenseAsync(CreateExpenseDto dto, int userId);
        Task<bool> CancelExpenseAsync(int id, int userId);
        Task<PagedResponse<Expense>> GetExpensesAsync(int pageNumber, int pageSize, bool? isActive, DateTime? startDate, DateTime? endDate);


        // --- 2. CUENTAS POR PAGAR (Crédito) ---
        Task<AccountsPayable> CreateAccountsPayableAsync(CreateAccountsPayableDto dto);
        Task<AccountsPayable> GetAccountsPayableByIdAsync(int id);
        Task<PagedResponse<AccountsPayable>> GetAccountsPayableAsync(int pageNumber, int pageSize, bool? isActive, DateTime? startDate, DateTime? endDate);

        // --- 3. CATEGORÍAS, PLAZOS Y RECORDATORIOS ---
        Task<List<ExpenseCategory>> GetExpenseCategoriesAsync(bool? isActive = true);
        Task<PagedResponse<PaymentSchedule>> GetPaymentSchedulesAsync(int pageNumber, int pageSize, int? accountsPayableId, string? status, DateTime? startDate, DateTime? endDate);
        Task<List<PaymentReminder>> GetPendingRemindersAsync();
    }
}