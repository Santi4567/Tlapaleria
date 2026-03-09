using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IPendingOrderService
    {
        Task<PendingOrder> CreatePendingOrderAsync(CreatePendingOrderDto datos, int userId); 
    }
}