using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IReturnService
    {
        //Buscar devoluciones 
        Task<PagedResponse<SaleReturn>> GetReturnsAsync(string? search = null, int pageNumber = 1, int pageSize = 50);

        //Crear Devolucion 
        Task<SaleReturn> CreateReturnAsync(CreateReturnDto dto, int userId);
    }
}