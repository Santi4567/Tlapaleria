using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IPendingOrderService
    {
        //Agregar productos a la tabla de pendientes 
        Task<PendingOrder> CreatePendingOrderAsync(CreatePendingOrderDto datos, int userId);
        //Mostrar Productos pendientes conforme al proveedor 
        Task<PagedResponse<PendingOrder>> GetPendingOrdersBySupplierAsync(int supplierId, int pageNumber = 1, int pageSize = 50);
        //Buscador por ID 
        Task<PendingOrder> GetPendingOrderByIdAsync(int id);
        //Buscador por nombre 
        Task<List<PendingOrder>> SearchPendingOrdersAsync(string searchTerm);
    }
}