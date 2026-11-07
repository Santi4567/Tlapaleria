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
    public class FacturasController : ControllerBase
    {
        private readonly IFacturacionService _facturacionService;

        public FacturasController(IFacturacionService facturacionService)
        {
            _facturacionService = facturacionService;
        }

        /// <summary>
        /// Emite y timbra una factura electrónica para una venta
        /// </summary>
        [HttpPost("emitir")]
        [RequierePermiso("manage.invoices", "add.sales")]
        public async Task<ActionResult<ApiResponse<FacturaResponseDto>>> Emitir([FromBody] EmitirFacturaDto dto)
        {
            try
            {
                var resultado = await _facturacionService.EmitirFacturaVentaAsync(dto);
                return Ok(ApiResponse<FacturaResponseDto>.Exito(resultado, "Factura emitida y timbrada exitosamente ante el SAT."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<FacturaResponseDto>.Error(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<FacturaResponseDto>.Error(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<FacturaResponseDto>.Error($"Error interno al procesar la factura: {ex.Message}"));
            }
        }

        /// <summary>
        /// Consulta la factura asociada a una venta
        /// </summary>
        [HttpGet("venta/{saleId}")]
        [RequierePermiso("manage.invoices", "view.sales")]
        public async Task<ActionResult<ApiResponse<Invoice>>> GetPorVenta(int saleId)
        {
            var factura = await _facturacionService.GetFacturaBySaleIdAsync(saleId);
            if (factura == null)
            {
                return NotFound(ApiResponse<Invoice>.Error("Esta venta no cuenta con factura emitida."));
            }
            return Ok(ApiResponse<Invoice>.Exito(factura));
        }

        /// <summary>
        /// Consulta una factura por su ID interno
        /// </summary>
        [HttpGet("{id}")]
        [RequierePermiso("manage.invoices", "view.sales")]
        public async Task<ActionResult<ApiResponse<Invoice>>> GetPorId(int id)
        {
            var factura = await _facturacionService.GetFacturaByIdAsync(id);
            if (factura == null)
            {
                return NotFound(ApiResponse<Invoice>.Error("Factura no encontrada."));
            }
            return Ok(ApiResponse<Invoice>.Exito(factura));
        }

        /// <summary>
        /// Descarga el PDF oficial de la factura
        /// </summary>
        [HttpGet("{id}/descargar-pdf")]
        [RequierePermiso("manage.invoices", "view.sales")]
        public async Task<IActionResult> DescargarPdf(int id)
        {
            var factura = await _facturacionService.GetFacturaByIdAsync(id);
            if (factura == null) return NotFound(ApiResponse<object>.Error("Factura no encontrada."));

            try
            {
                var pdfBytes = await _facturacionService.DescargarPdfAsync(factura.FacturapiId);
                return File(pdfBytes, "application/pdf", $"Factura_{factura.Uuid}.pdf");
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        /// <summary>
        /// Descarga el XML fiscal de la factura
        /// </summary>
        [HttpGet("{id}/descargar-xml")]
        [RequierePermiso("manage.invoices", "view.sales")]
        public async Task<IActionResult> DescargarXml(int id)
        {
            var factura = await _facturacionService.GetFacturaByIdAsync(id);
            if (factura == null) return NotFound(ApiResponse<object>.Error("Factura no encontrada."));

            try
            {
                var xmlBytes = await _facturacionService.DescargarXmlAsync(factura.FacturapiId);
                return File(xmlBytes, "application/xml", $"Factura_{factura.Uuid}.xml");
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        /// <summary>
        /// Envía o reenvía la factura por correo electrónico al cliente
        /// </summary>
        [HttpPost("{id}/enviar-email")]
        [RequierePermiso("manage.invoices", "add.sales")]
        public async Task<ActionResult<ApiResponse<bool>>> EnviarEmail(int id, [FromBody] EnviarEmailFacturaDto dto)
        {
            var factura = await _facturacionService.GetFacturaByIdAsync(id);
            if (factura == null) return NotFound(ApiResponse<bool>.Error("Factura no encontrada."));

            var enviado = await _facturacionService.EnviarPorEmailAsync(factura.FacturapiId, dto.Email);
            if (!enviado)
            {
                return BadRequest(ApiResponse<bool>.Error("No se pudo enviar el correo electrónico. Verifica la dirección."));
            }

            return Ok(ApiResponse<bool>.Exito(true, $"Factura enviada con éxito a {dto.Email}."));
        }

        /// <summary>
        /// Cancela una factura ante el SAT
        /// </summary>
        [HttpPost("{id}/cancelar")]
        [RequierePermiso("manage.invoices")]
        public async Task<ActionResult<ApiResponse<bool>>> Cancelar(int id, [FromBody] CancelarFacturaDto dto)
        {
            try
            {
                var cancelado = await _facturacionService.CancelarFacturaAsync(id, dto.Motivo);
                return Ok(ApiResponse<bool>.Exito(cancelado, "Factura cancelada exitosamente ante el SAT."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<bool>.Error(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<bool>.Error(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<bool>.Error($"Error al cancelar la factura: {ex.Message}"));
            }
        }
    }
}
