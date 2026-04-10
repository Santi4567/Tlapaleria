using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface ISaleService
    {
        // Nueva venta 
        Task<Sale> CreateSaleAsync(CreateSaleDto saleDto, int userId);
    }
}