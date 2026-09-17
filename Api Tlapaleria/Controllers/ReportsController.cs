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
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        // -- Reporte financiero de ventas --
        [HttpGet("financial")]
        [RequierePermiso("view.reports")]
        public async Task<ActionResult<ApiResponse<FinancialReportDto>>> GetFinancialReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var report = await _reportService.GetFinancialReportAsync(startDate, endDate);
            return Ok(ApiResponse<FinancialReportDto>.Exito(report, "Reporte financiero generado exitosamente."));
        }

        // -- Reporte/Historial de Precio de un producto (o una presentación específica) --
        [HttpGet("{id}/price-history")]
        [RequierePermiso("view.products")]
        public async Task<ActionResult<ApiResponse<ProductPriceHistoryDto>>> GetPriceHistory(
            int id,
            [FromQuery] int? presentationId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var historial = await _reportService.GetPriceHistoryAsync(id, presentationId, startDate, endDate);
            return Ok(ApiResponse<ProductPriceHistoryDto>.Exito(historial));
        }
    }
}