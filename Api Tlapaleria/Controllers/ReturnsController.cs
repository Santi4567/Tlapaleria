using Api_Tlapaleria.Attributes;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Api_Tlapaleria.Services;
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

        // GET: api/returns?search=DEV-123&page=1&pageSize=50
        [HttpGet]
        [RequierePermiso("view.returns")] // Permiso específico para ver este historial
        public async Task<ActionResult<ApiResponse<PagedResponse<SaleReturn>>>> GetReturns(
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                // Candados de seguridad para la paginación
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 100) pageSize = 100; // Máximo permitido por seguridad

                var historial = await _returnService.GetReturnsAsync(search, page, pageSize);

                return Ok(ApiResponse<PagedResponse<SaleReturn>>.Exito(historial, "Historial de devoluciones obtenido con éxito."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
    }
}