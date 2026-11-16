using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface ICustomerService
    {
        Task<PagedResponse<CustomerSummaryDto>> GetAllAsync(
            bool isActive = true,
            bool onlyWithDebt = false,
            string? search = null,
            int pageNumber = 1,
            int pageSize = 20);

        Task<List<CustomerSummaryDto>> SearchAsync(string termino, bool isActive = true);
        Task<CustomerSummaryDto> GetByIdAsync(int id);
        Task<CustomerFullProfileDto> GetCustomerFullProfileAsync(int customerId);
        Task<Customer> CreateAsync(CreateCustomerDto dto);
        Task<Customer> UpdateAsync(int id, UpdateCustomerDto dto);
        Task DeleteAsync(int id);
    }
}
