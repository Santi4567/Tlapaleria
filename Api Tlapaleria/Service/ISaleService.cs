using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface ISaleService
    {
        // Crear un nueva venta 
        Task<Sale> CreateSaleAsync(CreateSaleDto saleDto, int userId);

        // Buscador hibrido(Muestra todo/Buscador)
        Task<PagedResponse<Sale>> GetSalesAsync(string? searchFolio = null, int pageNumber = 1, int pageSize = 50);
    }
}