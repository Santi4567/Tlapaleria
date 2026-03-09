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
    [Authorize]
    public class PendingOrdersController : ControllerBase
    {
        private readonly IPendingOrderService _pendingOrderService;

        public PendingOrdersController(IPendingOrderService pendingOrderService)
        {
            _pendingOrderService = pendingOrderService;
        }

        // POST: api/pendingorders
        [HttpPost]
        [RequierePermiso("add.pendingorders")]
        public async Task<ActionResult<ApiResponse<PendingOrder>>> Create([FromBody] CreatePendingOrderDto datos)
        {
            try
            {
                // 1. Extraer el ID del JWT. 
                // Nota: Dependiendo de cómo generaste tu token en el Login, el claim puede llamarse 
                // ClaimTypes.NameIdentifier, "id", o "UserId". Ajusta el string si usaste uno personalizado.
                var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("id")?.Value;

                if (string.IsNullOrEmpty(claimId) || !int.TryParse(claimId, out int userIdToken))
                {
                    return Unauthorized(ApiResponse<object>.Error("Token inválido o no contiene la identidad del usuario."));
                }

                // 2. Pasarle el DTO y el ID extraído al servicio
                var nuevoPedido = await _pendingOrderService.CreatePendingOrderAsync(datos, userIdToken);

                return Ok(ApiResponse<PendingOrder>.Exito(nuevoPedido, "Faltante agregado a la libreta correctamente."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
        }
}