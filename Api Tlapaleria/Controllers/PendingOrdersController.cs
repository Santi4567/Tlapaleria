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
        // GET: api/pendingorders/supplier/1?page=1&pageSize=50
        [HttpGet("supplier/{supplierId}")]
        [Authorize]
        [RequierePermiso("view.pendingorders")] // Aquí está tu permiso sugerido
        public async Task<ActionResult<ApiResponse<PagedResponse<PendingOrder>>>> GetBySupplier(
            int supplierId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                // Validaciones básicas de seguridad para la paginación
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 100) pageSize = 100;

                var resultadoPaginado = await _pendingOrderService.GetPendingOrdersBySupplierAsync(supplierId, page, pageSize);

                return Ok(ApiResponse<PagedResponse<PendingOrder>>.Exito(resultadoPaginado, "Lista de pendientes obtenida correctamente."));
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

        // GET: api/pendingorders/search?query=clavos
        [HttpGet("search")]
        [Authorize]
        [RequierePermiso("view.pendingorders")]
        public async Task<ActionResult<ApiResponse<List<PendingOrder>>>> Search([FromQuery] string? query = "")
        {
            try
            {
                var pedidos = await _pendingOrderService.SearchPendingOrdersAsync(query);
                return Ok(ApiResponse<List<PendingOrder>>.Exito(pedidos, "Búsqueda completada"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
    }
}