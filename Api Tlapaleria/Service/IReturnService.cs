using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IReturnService
    {
        Task<PagedResponse<SaleReturn>> GetReturnsAsync(string? search = null, int pageNumber = 1, int pageSize = 50);
    }
}