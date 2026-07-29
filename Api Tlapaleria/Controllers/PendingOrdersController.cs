using Api_Tlapaleria.Attributes;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Api_Tlapaleria.Services;
using Api_Tlapaleria.Extensions; // ¡Importante importar el namespace del nuevo helper!
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
            // 1. Usamos nuestra nueva extensión DRY
            int userIdToken = User.GetUserId();

            // 2. Ejecutamos el servicio sin try-catch
            var nuevoPedido = await _pendingOrderService.CreatePendingOrderAsync(datos, userIdToken);

            return Ok(ApiResponse<PendingOrder>.Exito(nuevoPedido, "Faltante agregado a la libreta correctamente."));
        }

        // GET: api/pendingorders/supplier/1?status=Todos&page=1&pageSize=50
        [HttpGet("supplier/{supplierId}")]
        [Authorize]
        [RequierePermiso("view.pendingorders")]
        public async Task<ActionResult<ApiResponse<PagedResponse<PendingOrder>>>> GetBySupplier(
            int supplierId,
            [FromQuery] string status = "Pendiente",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var resultadoPaginado = await _pendingOrderService.GetPendingOrdersBySupplierAsync(supplierId, status, page, pageSize);

            return Ok(ApiResponse<PagedResponse<PendingOrder>>.Exito(resultadoPaginado));
        }

        // GET: api/pendingorders/{id}
        [HttpGet("{id}")]
        [Authorize]
        [RequierePermiso("view.pendingorders")]
        public async Task<ActionResult<ApiResponse<PendingOrder>>> GetById(int id)
        {
            var pedido = await _pendingOrderService.GetPendingOrderByIdAsync(id);
            return Ok(ApiResponse<PendingOrder>.Exito(pedido));
        }

        // GET: api/pendingorders/search?query=clavos&status=Todos&page=1&pageSize=50
        [HttpGet("search")]
        [Authorize]
        [RequierePermiso("view.pendingorders")]
        public async Task<ActionResult<ApiResponse<PagedResponse<PendingOrder>>>> Search(
            [FromQuery] string? query = "",
            [FromQuery] string status = "Todos",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var resultadosPaginados = await _pendingOrderService.SearchPendingOrdersAsync(query, status, page, pageSize);

            return Ok(ApiResponse<PagedResponse<PendingOrder>>.Exito(resultadosPaginados, "Búsqueda completada"));
        }

        // PUT: api/pendingorders/{id}
        [HttpPut("{id}")]
        [Authorize]
        [RequierePermiso("edit.pendingorders")]
        public async Task<ActionResult<ApiResponse<PendingOrder>>> Update(int id, [FromBody] UpdatePendingOrderDto datos)
        {
            int userIdToken = User.GetUserId();

            var pedidoActualizado = await _pendingOrderService.UpdatePendingOrderAsync(id, datos, userIdToken);

            return Ok(ApiResponse<PendingOrder>.Exito(pedidoActualizado, "El pedido ha sido actualizado correctamente."));
        }

        // PATCH: api/pendingorders/{id}/status
        [HttpPatch("{id}/status")]
        [Authorize]
        [RequierePermiso("edit.pendingorders")]
        public async Task<ActionResult<ApiResponse<PendingOrder>>> UpdateStatus(int id, [FromBody] UpdatePendingOrderStatusDto datos)
        {
            int userIdToken = User.GetUserId();

            var pedidoActualizado = await _pendingOrderService.UpdatePendingOrderStatusAsync(id, datos.Status, userIdToken);

            return Ok(ApiResponse<PendingOrder>.Exito(pedidoActualizado, $"El estado del pedido cambió exitosamente a '{datos.Status}'."));
        }
    }
}