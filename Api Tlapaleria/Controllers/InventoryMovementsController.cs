using Api_Tlapaleria.Attributes;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Api_Tlapaleria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api_Tlapaleria.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Blindado, nadie entra sin Token
    public class InventoryMovementsController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;

        public InventoryMovementsController(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        // POST: api/inventorymovements
        [HttpPost]
        [RequierePermiso("add.inventorymovements")] // El permiso sugerido para afectar el stock
        public async Task<ActionResult<ApiResponse<InventoryMovement>>> CreateMovement([FromBody] CreateInventoryMovementDto datos)
        {
            try
            {
                // 1. Extraer el ID del empleado que está haciendo el movimiento desde el JWT
                var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("id")?.Value;

                if (string.IsNullOrEmpty(claimId) || !int.TryParse(claimId, out int userIdToken))
                {
                    return Unauthorized(ApiResponse<object>.Error("Token inválido o no contiene la identidad del usuario."));
                }

                // 2. Ejecutar la transacción (Kardex + Actualización de Producto)
                var movimiento = await _inventoryService.RegisterMovementAsync(datos, userIdToken);

                return Ok(ApiResponse<InventoryMovement>.Exito(movimiento, "Movimiento de inventario registrado y stock actualizado correctamente."));
            }
            catch (Exception ex)
            {
                // Si alguien intenta hacer un movimiento inválido (ej. dejar el stock en negativo)
                // la transacción se cancela y devolvemos el error exacto aquí.
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
        // GET: api/inventorymovements/product/5?page=1&pageSize=20
        [HttpGet("product/{productId}")]
        [Authorize]
        [RequierePermiso("view.inventorymovements")] // Asignamos el permiso de lectura
        public async Task<ActionResult<ApiResponse<PagedResponse<InventoryMovement>>>> GetByProduct(
            int productId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                // Protecciones de paginación
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 100) pageSize = 100;

                var historial = await _inventoryService.GetMovementsByProductIdAsync(productId, page, pageSize);

                return Ok(ApiResponse<PagedResponse<InventoryMovement>>.Exito(historial, "Kardex obtenido correctamente."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
    }
}