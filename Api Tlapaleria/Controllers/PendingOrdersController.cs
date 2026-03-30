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
        // GET: api/pendingorders/supplier/1?status=Todos&page=1&pageSize=50
        [HttpGet("supplier/{supplierId}")]
        [Authorize]
        [RequierePermiso("view.pendingorders")]
        public async Task<ActionResult<ApiResponse<PagedResponse<PendingOrder>>>> GetBySupplier(
            int supplierId,
            [FromQuery] string status = "Pendiente", // Recibimos el estado dinámico
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 100) pageSize = 100;

                var resultadoPaginado = await _pendingOrderService.GetPendingOrdersBySupplierAsync(supplierId, status, page, pageSize);

                return Ok(ApiResponse<PagedResponse<PendingOrder>>.Exito(resultadoPaginado));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
        // GET: api/pendingorders/{id}
        [HttpGet("{id}")]
        [Authorize]
        [RequierePermiso("view.pendingorders")]
        public async Task<ActionResult<ApiResponse<PendingOrder>>> GetById(int id)
        {
            try
            {
                var pedido = await _pendingOrderService.GetPendingOrderByIdAsync(id);
                return Ok(ApiResponse<PendingOrder>.Exito(pedido));
            }
            catch (Exception ex)
            {
                // Usamos NotFound (404) cuando buscamos un ID exacto y no existe
                return NotFound(ApiResponse<object>.Error(ex.Message));
            }
        }

        // GET: api/pendingorders/search?query=clavos&status=Todos&page=1&pageSize=50
        [HttpGet("search")]
        [Authorize]
        [RequierePermiso("view.pendingorders")]
        public async Task<ActionResult<ApiResponse<PagedResponse<PendingOrder>>>> Search(
            [FromQuery] string? query = "",
            [FromQuery] string status = "Todos",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50) // Nuevos parámetros
        {
            try
            {
                // Protecciones para que no rompan la paginación
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 100) pageSize = 100; // Máximo 100 por petición por seguridad

                var resultadosPaginados = await _pendingOrderService.SearchPendingOrdersAsync(query, status, page, pageSize);

                return Ok(ApiResponse<PagedResponse<PendingOrder>>.Exito(resultadosPaginados, "Búsqueda completada"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
        // PUT: api/pendingorders/{id}
        [HttpPut("{id}")]
        [Authorize]
        [RequierePermiso("edit.pendingorders")] // Permiso específico para editar
        public async Task<ActionResult<ApiResponse<PendingOrder>>> Update(int id, [FromBody] UpdatePendingOrderDto datos)
        {
            try
            {
                // 1. Extraemos el ID del usuario desde el JWT
                var claimId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("id")?.Value;

                if (string.IsNullOrEmpty(claimId) || !int.TryParse(claimId, out int userIdToken))
                {
                    return Unauthorized(ApiResponse<object>.Error("Token inválido o no contiene la identidad del usuario."));
                }

                // 2. Ejecutamos la actualización
                var pedidoActualizado = await _pendingOrderService.UpdatePendingOrderAsync(id, datos, userIdToken);

                return Ok(ApiResponse<PendingOrder>.Exito(pedidoActualizado, "El pedido ha sido actualizado correctamente."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
        // PATCH: api/pendingorders/{id}/status
        [HttpPatch("{id}/status")]
        [Authorize]
        [RequierePermiso("edit.pendingorders")]
        public async Task<ActionResult<ApiResponse<PendingOrder>>> UpdateStatus(int id, [FromBody] UpdatePendingOrderStatusDto datos)
        {
            try
            {
                // 1. Extraemos quién está haciendo el Swipe
                var claimId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("id")?.Value;

                if (string.IsNullOrEmpty(claimId) || !int.TryParse(claimId, out int userIdToken))
                {
                    return Unauthorized(ApiResponse<object>.Error("Token inválido."));
                }

                // 2. Ejecutamos el cambio de estado
                var pedidoActualizado = await _pendingOrderService.UpdatePendingOrderStatusAsync(id, datos.Status, userIdToken);

                return Ok(ApiResponse<PendingOrder>.Exito(pedidoActualizado, $"El estado del pedido cambió exitosamente a '{datos.Status}'."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
    }
}