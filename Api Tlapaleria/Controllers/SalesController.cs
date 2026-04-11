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
    [Authorize] // Blindado
    public class SalesController : ControllerBase
    {
        private readonly ISaleService _saleService;

        public SalesController(ISaleService saleService)
        {
            _saleService = saleService;
        }

        // POST: api/sales
        [HttpPost]
        [RequierePermiso("add.sales")] // Solo los usuarios con rol de cajero/admin pueden cobrar
        public async Task<ActionResult<ApiResponse<Sale>>> CreateSale([FromBody] CreateSaleDto saleDto)
        {
            try
            {
                // 1. Extraemos quién está cobrando desde el Token JWT
                var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("id")?.Value;

                if (string.IsNullOrEmpty(claimId) || !int.TryParse(claimId, out int userIdToken))
                {
                    return Unauthorized(ApiResponse<object>.Error("Token inválido o identidad no encontrada."));
                }

                // 2. Ejecutamos la super transacción de venta
                var venta = await _saleService.CreateSaleAsync(saleDto, userIdToken);

                return Ok(ApiResponse<Sale>.Exito(venta, "¡Venta registrada exitosamente!"));
            }
            catch (Exception ex)
            {
                // Si falta stock, si la presentación no existe o la BD falla, 
                // rebotamos el error exacto al frontend sin guardar nada a medias.
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
    }
}