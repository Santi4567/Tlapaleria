using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IInventoryService
    {
        //Ajuste de stock en las tablas
        Task<InventoryMovement> RegisterMovementAsync(CreateInventoryMovementDto datos, int userId);
    }
}