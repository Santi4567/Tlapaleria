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
    }
}