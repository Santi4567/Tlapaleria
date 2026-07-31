using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Enums;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IInventoryService
    {
        //Ajuste de stock en las tablas
        Task<InventoryMovement> RegisterMovementAsync(CreateInventoryMovementDto datos, int userId);
        
        // Kardex de un producto por su ID 
        Task<PagedResponse<InventoryMovement>> GetMovementsByProductIdAsync(int productId, int pageNumber = 1, int pageSize = 50);

        // ---Buscador MÉTODO MAESTRO DE BÚSQUEDA 4 en 1 ---
        Task<PagedResponse<InventoryMovement>> GetMovementsAsync(
            int? productId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            MovementType? movementType = null,
            int pageNumber = 1,
            int pageSize = 50);
    }
}