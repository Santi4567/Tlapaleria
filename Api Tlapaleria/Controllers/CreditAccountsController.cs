using Api_Tlapaleria.Attributes;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Extensions;
using Api_Tlapaleria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Tlapaleria.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CreditAccountsController : ControllerBase
    {
        private readonly ICreditAccountService _creditAccountService;
        private readonly ISaleService _saleService;
        private readonly PermissionService _permissionService;

        public CreditAccountsController(
            ICreditAccountService creditAccountService,
            ISaleService saleService,
            PermissionService permissionService)
        {
            _creditAccountService = creditAccountService;
            _saleService = saleService;
            _permissionService = permissionService;
        }

        /// <summary>
        /// Resumen general de la sección de Cuentas por Cobrar (total en la calle, clientes con deuda, cuentas vencidas, cobrado hoy)
        /// </summary>
        [HttpGet("summary")]
        [RequierePermiso("view.credit_accounts", "manage.credit_accounts", "view.sales")]
        public async Task<ActionResult<ApiResponse<CreditPortfolioSummaryDto>>> GetPortfolioSummary()
        {
            var summary = await _creditAccountService.GetPortfolioSummaryAsync();
            return Ok(ApiResponse<CreditPortfolioSummaryDto>.Exito(summary, "Resumen de cartera de crédito obtenido."));
        }

        /// <summary>
        /// Obtiene el listado paginado de cuentas de crédito (con detalle de qué debe, cuándo adquirió la venta y cuánto se llevó)
        /// </summary>
        [HttpGet]
        [RequierePermiso("view.credit_accounts", "manage.credit_accounts", "view.sales")]
        public async Task<ActionResult<ApiResponse<PagedResponse<CreditAccountDetailDto>>>> GetAccounts(
            [FromQuery] int? customerId = null,
            [FromQuery] string? status = null,
            [FromQuery] bool onlyOverdue = false,
            [FromQuery] string? search = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var resultado = await _creditAccountService.GetAccountsAsync(customerId, status, onlyOverdue, search, pageNumber, pageSize);
            return Ok(ApiResponse<PagedResponse<CreditAccountDetailDto>>.Exito(resultado, "Cuentas de crédito obtenidas exitosamente."));
        }

        /// <summary>
        /// Obtiene el detalle completo de una cuenta por su ID (cliente, contacto, qué se llevó, cuándo, devoluciones y abonos)
        /// </summary>
        [HttpGet("{id}")]
        [RequierePermiso("view.credit_accounts", "manage.credit_accounts", "view.sales")]
        public async Task<ActionResult<ApiResponse<CreditAccountDetailDto>>> GetAccountById(int id)
        {
            var cuenta = await _creditAccountService.GetAccountByIdAsync(id);
            return Ok(ApiResponse<CreditAccountDetailDto>.Exito(cuenta));
        }

        /// <summary>
        /// Obtiene la cuenta de crédito ligada a un ticket de venta específico
        /// </summary>
        [HttpGet("sale/{saleId}")]
        [RequierePermiso("view.credit_accounts", "manage.credit_accounts", "view.sales")]
        public async Task<ActionResult<ApiResponse<CreditAccountDetailDto>>> GetAccountBySaleId(int saleId)
        {
            var cuenta = await _creditAccountService.GetAccountBySaleIdAsync(saleId);
            if (cuenta == null)
            {
                return NotFound(ApiResponse<CreditAccountDetailDto>.Error($"El ticket con ID {saleId} no tiene una cuenta de crédito asociada."));
            }
            return Ok(ApiResponse<CreditAccountDetailDto>.Exito(cuenta));
        }

        /// <summary>
        /// Registra una nueva venta a crédito directamente desde la sección de Cuentas y devuelve la cuenta detallada creada
        /// </summary>
        [HttpPost("sale")]
        [RequierePermiso("manage.credit_accounts", "add.sales")]
        public async Task<ActionResult<ApiResponse<CreditAccountDetailDto>>> CreateCreditSale([FromBody] CreateSaleDto saleDto)
        {
            int userIdToken = User.GetUserId();

            // Validación de permiso de descuento si el ticket incluye descuento
            bool pideDescuento = (saleDto.DiscountPercent ?? 0) > 0 || (saleDto.DiscountAmount ?? 0) > 0;
            if (pideDescuento)
            {
                var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
                if (!await _permissionService.UserHasPermissionAsync(userRole, "apply.discounts"))
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        ApiResponse<CreditAccountDetailDto>.Error("No tienes permisos para aplicar descuentos. Requieres: apply.discounts"));
                }
            }

            saleDto.PaymentMethod = "Credito";
            var venta = await _saleService.CreateSaleAsync(saleDto, userIdToken);
            var cuentaCreada = await _creditAccountService.GetAccountBySaleIdAsync(venta.Id);

            return Ok(ApiResponse<CreditAccountDetailDto>.Exito(cuentaCreada!, "¡Venta a crédito y cuenta por cobrar registradas exitosamente!"));
        }

        /// <summary>
        /// Registra un abono directo a una cuenta específica (por ticket)
        /// </summary>
        [HttpPost("{id}/payments")]
        [RequierePermiso("manage.credit_accounts", "add.sales")]
        public async Task<ActionResult<ApiResponse<CreditAccountDetailDto>>> RegisterAccountPayment(
            int id,
            [FromBody] RegisterCreditPaymentDto dto)
        {
            int userIdToken = User.GetUserId();
            var cuentaActualizada = await _creditAccountService.RegisterAccountPaymentAsync(id, dto, userIdToken);

            string mensaje = cuentaActualizada.Balance == 0
                ? $"¡Abono registrado! La cuenta {cuentaActualizada.Folio} ha quedado totalmente LIQUIDADA."
                : $"Abono registrado exitosamente. Saldo restante de esta cuenta: ${cuentaActualizada.Balance:N2}.";

            return Ok(ApiResponse<CreditAccountDetailDto>.Exito(cuentaActualizada, mensaje));
        }

        /// <summary>
        /// Registra un abono al saldo general del cliente (reparte el dinero automáticamente desde la cuenta más antigua a la más nueva)
        /// </summary>
        [HttpPost("customer/{customerId}/pay-general")]
        [RequierePermiso("manage.credit_accounts", "add.sales")]
        public async Task<ActionResult<ApiResponse<GeneralCustomerPaymentResultDto>>> RegisterGeneralCustomerPayment(
            int customerId,
            [FromBody] RegisterCreditPaymentDto dto)
        {
            int userIdToken = User.GetUserId();
            var resultado = await _creditAccountService.RegisterGeneralCustomerPaymentAsync(customerId, dto, userIdToken);

            string mensaje = resultado.RemainingTotalDebt == 0
                ? $"¡Abono general de ${resultado.TotalAmountApplied:N2} aplicado! El cliente '{resultado.CustomerName}' ha liquidado toda su deuda."
                : $"Abono general de ${resultado.TotalAmountApplied:N2} aplicado a {resultado.AccountsAffectedCount} cuenta(s). Deuda restante del cliente: ${resultado.RemainingTotalDebt:N2}.";

            return Ok(ApiResponse<GeneralCustomerPaymentResultDto>.Exito(resultado, mensaje));
        }

        /// <summary>
        /// Actualiza la fecha compromiso de pago o las notas de una cuenta de crédito
        /// </summary>
        [HttpPut("{id}")]
        [RequierePermiso("manage.credit_accounts", "add.sales")]
        public async Task<ActionResult<ApiResponse<CreditAccountDetailDto>>> UpdateAccountMetadata(
            int id,
            [FromBody] UpdateCreditAccountDto dto)
        {
            var actualizada = await _creditAccountService.UpdateAccountMetadataAsync(id, dto);
            return Ok(ApiResponse<CreditAccountDetailDto>.Exito(actualizada, "Datos de la cuenta actualizados correctamente."));
        }

        /// <summary>
        /// Obtiene el historial paginado de abonos recibidos (filtrable por cliente, cuenta o fechas)
        /// </summary>
        [HttpGet("payments")]
        [RequierePermiso("view.credit_accounts", "manage.credit_accounts", "view.sales")]
        public async Task<ActionResult<ApiResponse<PagedResponse<CreditPaymentDto>>>> GetPaymentsHistory(
            [FromQuery] int? customerId = null,
            [FromQuery] int? accountId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 30)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 30;
            if (pageSize > 100) pageSize = 100;

            var historial = await _creditAccountService.GetPaymentsHistoryAsync(customerId, accountId, startDate, endDate, pageNumber, pageSize);
            return Ok(ApiResponse<PagedResponse<CreditPaymentDto>>.Exito(historial, "Historial de abonos obtenido."));
        }
    }
}
