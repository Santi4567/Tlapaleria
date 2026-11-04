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
    public class SalesController : ControllerBase
    {
        private readonly ISaleService _saleService;
        private readonly PermissionService _permissionService;

        public SalesController(ISaleService saleService, PermissionService permissionService)
        {
            _saleService = saleService;
            _permissionService = permissionService;
        }

        [HttpPost]
        [RequierePermiso("add.sales")]
        public async Task<ActionResult<ApiResponse<Sale>>> CreateSale([FromBody] CreateSaleDto saleDto)
        {
            int userIdToken = User.GetUserId();

            // Solo roles con el permiso 'apply.discounts' pueden vender con descuento
            bool pideDescuento = (saleDto.DiscountPercent ?? 0) > 0 || (saleDto.DiscountAmount ?? 0) > 0;
            if (pideDescuento)
            {
                var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
                if (!await _permissionService.UserHasPermissionAsync(userRole, "apply.discounts"))
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        ApiResponse<Sale>.Error("No tienes permisos para aplicar descuentos. Requieres: apply.discounts"));
                }
            }

            var venta = await _saleService.CreateSaleAsync(saleDto, userIdToken);

            return Ok(ApiResponse<Sale>.Exito(venta, "¡Venta registrada exitosamente!"));
        }

        [HttpGet]
        [RequierePermiso("view.sales")]
        public async Task<ActionResult<ApiResponse<PagedResponse<Sale>>>> GetSales(
            [FromQuery] string? searchFolio = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var historialVentas = await _saleService.GetSalesAsync(searchFolio, page, pageSize);

            return Ok(ApiResponse<PagedResponse<Sale>>.Exito(historialVentas, "Historial de ventas obtenido."));
        }

        [HttpGet("{id}")]
        [RequierePermiso("view.sales")]
        public async Task<ActionResult<ApiResponse<Sale>>> GetSaleById(int id)
        {
            var ticket = await _saleService.GetSaleByIdAsync(id);

            // Mantenemos esta validación lógica manual si no está dentro de tu servicio
            if (ticket == null)
            {
                return NotFound(ApiResponse<object>.Error($"No se encontró ningún ticket con el ID {id}."));
            }

            return Ok(ApiResponse<Sale>.Exito(ticket, "Ticket recuperado exitosamente."));
        }
    }
}