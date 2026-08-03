using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Api_Tlapaleria.Enums;

namespace Api_Tlapaleria.Services
{
    public interface IPendingOrderService
    {
        Task<PendingOrder> CreatePendingOrderAsync(CreatePendingOrderDto datos, int userId);

        Task<PendingOrder> GetPendingOrderByIdAsync(int id);

        // EL ENDPOINT MAESTRO QUE REEMPLAZA A TODOS LOS DEMÁS GETs (Excepto GetById)
        Task<PagedResponse<PendingOrder>> GetAdvancedPendingOrdersAsync(
            string? search = null,
            int? supplierId = null,
            int? productId = null,
            PendingOrderStatus? status = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int pageNumber = 1,
            int pageSize = 50);

        Task<PendingOrder> UpdatePendingOrderAsync(int id, UpdatePendingOrderDto datos, int userId);

        Task<PendingOrder> UpdatePendingOrderStatusAsync(int id, PendingOrderStatus status, int userId);
    }
}