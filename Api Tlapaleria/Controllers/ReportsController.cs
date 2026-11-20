using Api_Tlapaleria.Attributes;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Enums;
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
            [FromQuery] DateTime? endDate = null,
            [FromQuery] FinancialReportPeriod periodType = FinancialReportPeriod.CustomRange)
        {
            var report = await _reportService.GetFinancialReportAsync(startDate, endDate, periodType);
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

        // -- Reporte de ventas por proveedor (separación de dinero de proveedores y ganancia) --
        [HttpGet("suppliers-sales")]
        [RequierePermiso("view.reports")]
        public async Task<ActionResult<ApiResponse<SupplierSalesReportResponseDto>>> GetSupplierSalesReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] SupplierReportPeriod periodType = SupplierReportPeriod.AllTime)
        {
            var report = await _reportService.GetSupplierSalesReportAsync(startDate, endDate, periodType);
            return Ok(ApiResponse<SupplierSalesReportResponseDto>.Exito(report, "Reporte de ventas por proveedor generado exitosamente."));
        }

        // -- Desglose de ventas por producto/presentación de un proveedor específico --
        [HttpGet("suppliers-sales/{supplierId}")]
        [RequierePermiso("view.reports")]
        public async Task<ActionResult<ApiResponse<SupplierSalesDetailResponseDto>>> GetSupplierSalesDetail(
            int supplierId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] SupplierReportPeriod periodType = SupplierReportPeriod.AllTime)
        {
            var detail = await _reportService.GetSupplierSalesDetailAsync(supplierId, startDate, endDate, periodType);
            return Ok(ApiResponse<SupplierSalesDetailResponseDto>.Exito(detail, "Desglose de ventas del proveedor generado exitosamente."));
        }

        // -- Reporte de ventas por usuario (qué vendió cada usuario) --
        [HttpGet("users-sales")]
        [RequierePermiso("view.reports")]
        public async Task<ActionResult<ApiResponse<UserSalesReportResponseDto>>> GetUsersSalesReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] UserSalesReportPeriod periodType = UserSalesReportPeriod.Day,
            [FromQuery] int? userId = null)
        {
            var report = await _reportService.GetUsersSalesReportAsync(startDate, endDate, periodType, userId);
            return Ok(ApiResponse<UserSalesReportResponseDto>.Exito(report, "Reporte de ventas por usuario generado exitosamente."));
        }

        // -- Desglose de lo que vendió un usuario específico --
        [HttpGet("users-sales/{userId}")]
        [RequierePermiso("view.reports")]
        public async Task<ActionResult<ApiResponse<UserSalesDetailResponseDto>>> GetUserSalesDetail(
            int userId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] UserSalesReportPeriod periodType = UserSalesReportPeriod.Day)
        {
            var detail = await _reportService.GetUserSalesDetailAsync(userId, startDate, endDate, periodType);
            return Ok(ApiResponse<UserSalesDetailResponseDto>.Exito(detail, "Detalle de ventas del usuario generado exitosamente."));
        }
    }
}
