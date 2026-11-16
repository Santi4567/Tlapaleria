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
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomersController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        /// <summary>
        /// Obtiene el listado paginado de clientes con su información de contacto y deuda actual
        /// </summary>
        [HttpGet]
        [RequierePermiso("view.customers", "view.credit_accounts", "view.sales", "add.sales")]
        public async Task<ActionResult<ApiResponse<PagedResponse<CustomerSummaryDto>>>> GetAll(
            [FromQuery] bool isActive = true,
            [FromQuery] bool onlyWithDebt = false,
            [FromQuery] string? search = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var resultado = await _customerService.GetAllAsync(isActive, onlyWithDebt, search, pageNumber, pageSize);
            return Ok(ApiResponse<PagedResponse<CustomerSummaryDto>>.Exito(resultado, "Listado de clientes obtenido correctamente."));
        }

        /// <summary>
        /// Buscador rápido de clientes por nombre, apodo, teléfono o dirección
        /// </summary>
        [HttpGet("search/{termino}")]
        [RequierePermiso("view.customers", "view.credit_accounts", "view.sales", "add.sales")]
        public async Task<ActionResult<ApiResponse<List<CustomerSummaryDto>>>> Search(
            string termino,
            [FromQuery] bool isActive = true)
        {
            var resultados = await _customerService.SearchAsync(termino, isActive);
            return Ok(ApiResponse<List<CustomerSummaryDto>>.Exito(resultados));
        }

        /// <summary>
        /// Obtiene la información de contacto y resumen de deuda de un cliente por su ID
        /// </summary>
        [HttpGet("{id}")]
        [RequierePermiso("view.customers", "view.credit_accounts", "view.sales", "add.sales")]
        public async Task<ActionResult<ApiResponse<CustomerSummaryDto>>> GetById(int id)
        {
            var cliente = await _customerService.GetByIdAsync(id);
            return Ok(ApiResponse<CustomerSummaryDto>.Exito(cliente));
        }

        /// <summary>
        /// Obtiene el expediente completo del cliente: contacto, deuda total, todas sus cuentas (qué se llevó, cuándo y cuánto debe) y sus abonos
        /// </summary>
        [HttpGet("{id}/profile")]
        [RequierePermiso("view.customers", "view.credit_accounts", "view.sales")]
        public async Task<ActionResult<ApiResponse<CustomerFullProfileDto>>> GetFullProfile(int id)
        {
            var expediente = await _customerService.GetCustomerFullProfileAsync(id);
            return Ok(ApiResponse<CustomerFullProfileDto>.Exito(expediente, "Expediente completo del cliente obtenido."));
        }

        /// <summary>
        /// Registra un nuevo cliente de confianza
        /// </summary>
        [HttpPost]
        [RequierePermiso("add.customers", "manage.credit_accounts", "add.sales")]
        public async Task<ActionResult<ApiResponse<Customer>>> Create([FromBody] CreateCustomerDto dto)
        {
            var creado = await _customerService.CreateAsync(dto);
            return Ok(ApiResponse<Customer>.Exito(creado, "Cliente registrado correctamente."));
        }

        /// <summary>
        /// Actualiza la información de contacto, notas o límite de crédito de un cliente
        /// </summary>
        [HttpPut("{id}")]
        [RequierePermiso("edit.customers", "manage.credit_accounts")]
        public async Task<ActionResult<ApiResponse<Customer>>> Update(int id, [FromBody] UpdateCustomerDto dto)
        {
            var actualizado = await _customerService.UpdateAsync(id, dto);
            return Ok(ApiResponse<Customer>.Exito(actualizado, "Información del cliente actualizada correctamente."));
        }

        /// <summary>
        /// Desactiva un cliente (solo si no tiene cuentas con deuda pendiente)
        /// </summary>
        [HttpDelete("{id}")]
        [RequierePermiso("delete.customers", "manage.credit_accounts")]
        public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
        {
            await _customerService.DeleteAsync(id);
            return Ok(ApiResponse<object>.Exito(null, "Cliente desactivado correctamente."));
        }
    }
}
