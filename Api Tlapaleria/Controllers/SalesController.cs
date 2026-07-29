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

        public SalesController(ISaleService saleService)
        {
            _saleService = saleService;
        }

        [HttpPost]
        [RequierePermiso("add.sales")]
        public async Task<ActionResult<ApiResponse<Sale>>> CreateSale([FromBody] CreateSaleDto saleDto)
        {
            int userIdToken = User.GetUserId();
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