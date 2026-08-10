using Api_Tlapaleria.Attributes;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Api_Tlapaleria.Services;
using Api_Tlapaleria.Extensions;
using Api_Tlapaleria.Enums;
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
            int userIdToken = User.GetUserId();
            var nuevoPedido = await _pendingOrderService.CreatePendingOrderAsync(datos, userIdToken);
            return Ok(ApiResponse<PendingOrder>.Exito(nuevoPedido, "Faltante agregado a la libreta correctamente."));
        }

        // GET: api/pendingorders/{id}
        // (Mantenemos este porque buscar un solo registro por ID es indispensable)
        [HttpGet("{id}")]
        [Authorize]
        [RequierePermiso("view.pendingorders")]
        public async Task<ActionResult<ApiResponse<PendingOrder>>> GetById(int id)
        {
            var pedido = await _pendingOrderService.GetPendingOrderByIdAsync(id);
            return Ok(ApiResponse<PendingOrder>.Exito(pedido));
        }

        // GET: api/pendingorders/filter
        // EL ENDPOINT MAESTRO QUE HACE TODO LO DEMÁS
        [HttpGet("filter")]
        [Authorize]
        [RequierePermiso("view.pendingorders")]
        public async Task<ActionResult<ApiResponse<PagedResponse<PendingOrder>>>> GetAdvancedFilters(
            [FromQuery] string? search = null,
            [FromQuery] int? supplierId = null,
            [FromQuery] int? productId = null,
            [FromQuery] PendingOrderStatus? status = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var resultadosPaginados = await _pendingOrderService.GetAdvancedPendingOrdersAsync(
                search, supplierId, productId, status, startDate, endDate, page, pageSize);

            return Ok(ApiResponse<PagedResponse<PendingOrder>>.Exito(resultadosPaginados, "Filtros aplicados exitosamente"));
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

        // GET: api/pendingorders/{id}/history
        [HttpGet("{id}/history")]
        [Authorize]
        [RequierePermiso("view.pendingorders")]
        public async Task<ActionResult<ApiResponse<List<PendingOrderHistoryDto>>>> GetHistory(int id)
        {
            var historial = await _pendingOrderService.GetPendingOrderHistoryAsync(id);
            return Ok(ApiResponse<List<PendingOrderHistoryDto>>.Exito(historial, "Historial recuperado exitosamente."));
        }
    }
}