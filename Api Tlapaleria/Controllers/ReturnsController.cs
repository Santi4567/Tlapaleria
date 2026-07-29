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
    public class ReturnsController : ControllerBase
    {
        private readonly IReturnService _returnService;

        public ReturnsController(IReturnService returnService)
        {
            _returnService = returnService;
        }

        [HttpGet]
        [RequierePermiso("view.returns")]
        public async Task<ActionResult<ApiResponse<PagedResponse<SaleReturn>>>> GetReturns(
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var historial = await _returnService.GetReturnsAsync(search, page, pageSize);

            return Ok(ApiResponse<PagedResponse<SaleReturn>>.Exito(historial, "Historial de devoluciones obtenido con éxito."));
        }

        [HttpPost]
        [RequierePermiso("add.returns")]
        public async Task<ActionResult<ApiResponse<SaleReturn>>> CreateReturn([FromBody] CreateReturnDto returnDto)
        {
            int userIdToken = User.GetUserId();
            var devolucion = await _returnService.CreateReturnAsync(returnDto, userIdToken);

            return Ok(ApiResponse<SaleReturn>.Exito(devolucion, "¡Devolución registrada y stock actualizado con éxito!"));
        }

        [HttpGet("sale/{saleId}")]
        [RequierePermiso("view.returns")]
        public async Task<ActionResult<ApiResponse<List<SaleReturn>>>> GetReturnsBySaleId(int saleId)
        {
            var devoluciones = await _returnService.GetReturnsBySaleIdAsync(saleId);
            return Ok(ApiResponse<List<SaleReturn>>.Exito(devoluciones, "Historial de devoluciones del ticket obtenido."));
        }
    }
}