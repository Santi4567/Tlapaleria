using Api_Tlapaleria.Attributes;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Api_Tlapaleria.Services;
using Api_Tlapaleria.Extensions; // Agregado para GetUserId()
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Tlapaleria.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InventoryMovementsController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;

        public InventoryMovementsController(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        [HttpPost]
        [RequierePermiso("add.inventorymovements")]
        public async Task<ActionResult<ApiResponse<InventoryMovement>>> CreateMovement([FromBody] CreateInventoryMovementDto datos)
        {
            // Extracción limpia del usuario
            int userIdToken = User.GetUserId();

            var movimiento = await _inventoryService.RegisterMovementAsync(datos, userIdToken);

            return Ok(ApiResponse<InventoryMovement>.Exito(movimiento, "Movimiento de inventario registrado y stock actualizado correctamente."));
        }

        [HttpGet("product/{productId}")]
        [Authorize]
        [RequierePermiso("view.inventorymovements")]
        public async Task<ActionResult<ApiResponse<PagedResponse<InventoryMovement>>>> GetByProduct(
            int productId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var historial = await _inventoryService.GetMovementsByProductIdAsync(productId, page, pageSize);

            return Ok(ApiResponse<PagedResponse<InventoryMovement>>.Exito(historial, "Kardex obtenido correctamente."));
        }
    }
}