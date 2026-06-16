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

        // POST: api/returns
        [HttpPost]
        [RequierePermiso("add.returns")] // Permiso específico (quizás solo para gerentes o cajeros autorizados)
        public async Task<ActionResult<ApiResponse<SaleReturn>>> CreateReturn([FromBody] CreateReturnDto returnDto)
        {
            try
            {
                // 1. Extraemos quién está autorizando la devolución desde el Token JWT
                var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("id")?.Value;

                if (string.IsNullOrEmpty(claimId) || !int.TryParse(claimId, out int userIdToken))
                {
                    return Unauthorized(ApiResponse<object>.Error("Token inválido o identidad no encontrada."));
                }

                // 2. Ejecutamos la transacción en la bóveda
                var devolucion = await _returnService.CreateReturnAsync(returnDto, userIdToken);

                return Ok(ApiResponse<SaleReturn>.Exito(devolucion, "¡Devolución registrada y stock actualizado con éxito!"));
            }
            catch (Exception ex)
            {
                // Si intenta devolver más de lo comprado, el servicio lanzará la excepción y caerá aquí
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        // GET: api/returns/sale/10
        [HttpGet("sale/{saleId}")]
        [RequierePermiso("view.returns")] // Permiso de lectura de devoluciones
        public async Task<ActionResult<ApiResponse<List<SaleReturn>>>> GetReturnsBySaleId(int saleId)
        {
            try
            {
                var devoluciones = await _returnService.GetReturnsBySaleIdAsync(saleId);

                // Si no hay devoluciones, regresamos una lista vacía con estatus 200 (es un flujo normal)
                return Ok(ApiResponse<List<SaleReturn>>.Exito(devoluciones, "Historial de devoluciones del ticket obtenido."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
    }
}