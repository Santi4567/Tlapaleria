using Api_Tlapaleria.Attributes;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Api_Tlapaleria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Api_Tlapaleria.Extensions;

namespace Api_Tlapaleria.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ExpensesController : ControllerBase
    {
        private readonly IExpenseService _expenseService;

        public ExpensesController(IExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        // ==============================================================================
        // 1. EGRESOS (Transacciones de salida de dinero real)
        // ==============================================================================

        [HttpPost]
        [RequierePermiso("add.expenses")]
        public async Task<ActionResult<ApiResponse<Expense>>> CreateExpense([FromBody] CreateExpenseDto datos)
        {
            // Sacamos el ID del cajero/usuario desde el token
            int userIdToken = User.GetUserId();

            var egresoCreado = await _expenseService.CreateExpenseAsync(datos, userIdToken);
            return Ok(ApiResponse<Expense>.Exito(egresoCreado, "Egreso registrado exitosamente."));
        }

        [HttpPut("{id}/cancel")]
        [RequierePermiso("delete.expenses")]
        public async Task<ActionResult<ApiResponse<bool>>> CancelExpense(int id)
        {
            // Usamos verbo PUT (o PATCH) porque es un borrado lógico (IsActive = false), no un DELETE real
            int userIdToken = User.GetUserId();

            var fueCancelado = await _expenseService.CancelExpenseAsync(id, userIdToken);
            return Ok(ApiResponse<bool>.Exito(fueCancelado, "Egreso anulado. Los saldos y plazos han sido revertidos en automático."));
        }

        // ==============================================================================
        // 2. CUENTAS POR PAGAR (Deudas/Crédito de Proveedores)
        // ==============================================================================

        [HttpPost("accounts-payable")]
        [RequierePermiso("add.expenses")]
        public async Task<ActionResult<ApiResponse<AccountsPayable>>> CreateAccountsPayable([FromBody] CreateAccountsPayableDto datos)
        {
            var deudaCreada = await _expenseService.CreateAccountsPayableAsync(datos);
            return Ok(ApiResponse<AccountsPayable>.Exito(deudaCreada, "Cuenta por pagar registrada y plazos generados."));
        }

        [HttpGet("accounts-payable/{id}")]
        [RequierePermiso("view.expenses")]
        public async Task<ActionResult<ApiResponse<AccountsPayable>>> GetAccountsPayableById(int id)
        {
            var deuda = await _expenseService.GetAccountsPayableByIdAsync(id);
            return Ok(ApiResponse<AccountsPayable>.Exito(deuda));
        }

        // ==============================================================================
        // 3. TRAER LOS REGISTROS
        // ==============================================================================

        [HttpGet]
        [RequierePermiso("view.expenses")]
        public async Task<ActionResult<ApiResponse<PagedResponse<Expense>>>> GetExpenses(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] bool? isActive = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var resultado = await _expenseService.GetExpensesAsync(page, pageSize, isActive, startDate, endDate);
            return Ok(ApiResponse<PagedResponse<Expense>>.Exito(resultado));
        }

        [HttpGet("accounts-payable")]
        [RequierePermiso("view.expenses")]
        public async Task<ActionResult<ApiResponse<PagedResponse<AccountsPayable>>>> GetAccountsPayables(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] bool? isActive = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var resultado = await _expenseService.GetAccountsPayableAsync(page, pageSize, isActive, startDate, endDate);
            return Ok(ApiResponse<PagedResponse<AccountsPayable>>.Exito(resultado));
        }

        // ==============================================================================
        // 4. RUTAS AUXILIARES (Categorías, Plazos y Recordatorios)
        // ==============================================================================

        [HttpGet("categories")]
        [RequierePermiso("view.expenses")]
        public async Task<ActionResult<ApiResponse<List<ExpenseCategory>>>> GetCategories([FromQuery] bool? isActive = true)
        {
            var categorias = await _expenseService.GetExpenseCategoriesAsync(isActive);
            return Ok(ApiResponse<List<ExpenseCategory>>.Exito(categorias));
        }

        [HttpGet("schedules")]
        [RequierePermiso("view.expenses")]
        public async Task<ActionResult<ApiResponse<PagedResponse<PaymentSchedule>>>> GetSchedules(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] int? accountsPayableId = null,
            [FromQuery] string? status = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var resultado = await _expenseService.GetPaymentSchedulesAsync(page, pageSize, accountsPayableId, status, startDate, endDate);
            return Ok(ApiResponse<PagedResponse<PaymentSchedule>>.Exito(resultado));
        }

        [HttpGet("reminders/pending")]
        [RequierePermiso("view.expenses")]
        public async Task<ActionResult<ApiResponse<List<PaymentReminder>>>> GetPendingReminders()
        {
            var recordatorios = await _expenseService.GetPendingRemindersAsync();
            return Ok(ApiResponse<List<PaymentReminder>>.Exito(recordatorios));
        }
    }
}