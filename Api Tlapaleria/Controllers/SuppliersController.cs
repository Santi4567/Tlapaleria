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
    public class SuppliersController : ControllerBase
    {
        private readonly ISupplierService _supplierService;

        public SuppliersController(ISupplierService supplierService)
        {
            _supplierService = supplierService;
        }

        [HttpGet]
        [RequierePermiso("view.suppliers")]
        public async Task<ActionResult<ApiResponse<PagedResponse<Supplier>>>> GetAll(
            [FromQuery] bool isActive = true,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var resultado = await _supplierService.GetAllAsync(isActive, pageNumber, pageSize);
            return Ok(ApiResponse<PagedResponse<Supplier>>.Exito(resultado));
        }

        [HttpGet("search/{termino}")]
        [RequierePermiso("view.suppliers")]
        public async Task<ActionResult<ApiResponse<List<Supplier>>>> Search(string termino, [FromQuery] bool isActive = true)
        {
            var resultados = await _supplierService.SearchAsync(termino, isActive);
            return Ok(ApiResponse<List<Supplier>>.Exito(resultados));
        }

        [HttpPost]
        [RequierePermiso("add.suppliers")]
        public async Task<ActionResult<ApiResponse<Supplier>>> Create([FromBody] CreateSupplierDto datos)
        {
            var creado = await _supplierService.CreateAsync(datos);
            return Ok(ApiResponse<Supplier>.Exito(creado, "Proveedor registrado correctamente"));
        }

        [HttpPut("{id}")]
        [RequierePermiso("edit.suppliers")]
        public async Task<ActionResult<ApiResponse<Supplier>>> Update(int id, [FromBody] UpdateSupplierDto datos)
        {
            var actualizado = await _supplierService.UpdateAsync(id, datos);
            return Ok(ApiResponse<Supplier>.Exito(actualizado, "Proveedor actualizado correctamente"));
        }

        [HttpDelete("{id}")]
        [RequierePermiso("delete.suppliers")]
        public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
        {
            await _supplierService.DeleteAsync(id);
            return Ok(ApiResponse<object>.Exito(null, "Proveedor eliminado (desactivado) correctamente"));
        }
    }
}