using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IPendingOrderService
    {
        //Agregar productos a la tabla de pendientes 
        Task<PendingOrder> CreatePendingOrderAsync(CreatePendingOrderDto datos, int userId);
        //Mostrar Productos pendientes conforme al proveedor y su estado 
        Task<PagedResponse<PendingOrder>> GetPendingOrdersBySupplierAsync(int supplierId, string status = "Pendiente", int pageNumber = 1, int pageSize = 50);
        //Buscador por ID 
        Task<PendingOrder> GetPendingOrderByIdAsync(int id);
        //Buscador por nombre,codigo de barras implementando el estado 
        Task<PagedResponse<PendingOrder>> SearchPendingOrdersAsync(string searchTerm, string status = "Todos", int pageNumber = 1, int pageSize = 50);
        // Actualizacion de datos
        Task<PendingOrder> UpdatePendingOrderAsync(int id, UpdatePendingOrderDto datos, int userId);
        //Actualizar estatus 
        Task<PendingOrder> UpdatePendingOrderStatusAsync(int id, string status, int userId);
    }
}