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
    [Authorize] // Todos requieren token
    public class SuppliersController : ControllerBase
    {
        private readonly ISupplierService _supplierService;

        public SuppliersController(ISupplierService supplierService)
        {
            _supplierService = supplierService;
        }

        // 1. OBTENER TODOS
        [HttpGet]
        [RequierePermiso("view.suppliers")]
        public async Task<ActionResult<ApiResponse<List<Supplier>>>> GetAll()
        {
            var lista = await _supplierService.GetAllAsync();
            return Ok(ApiResponse<List<Supplier>>.Exito(lista));
        }

        // 2. BUSCAR POR NOMBRE
        [HttpGet("search/{termino}")]
        [RequierePermiso("view.suppliers")]
        public async Task<ActionResult<ApiResponse<List<Supplier>>>> Search(string termino)
        {
            var resultados = await _supplierService.SearchAsync(termino);
            return Ok(ApiResponse<List<Supplier>>.Exito(resultados));
        }

        // 3. CREAR
        [HttpPost]
        [RequierePermiso("add.suppliers")]
        public async Task<ActionResult<ApiResponse<Supplier>>> Create([FromBody] CreateSupplierDto datos)
        {
            try
            {
                var creado = await _supplierService.CreateAsync(datos);
                return Ok(ApiResponse<Supplier>.Exito(creado, "Proveedor registrado correctamente"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message)); // Aquí saldrá el error de "Ya existe"
            }
        }

        // 4. ACTUALIZAR
        [HttpPut("{id}")]
        [RequierePermiso("edit.suppliers")]
        public async Task<ActionResult<ApiResponse<Supplier>>> Update(int id, [FromBody] UpdateSupplierDto datos)
        {
            try
            {
                var actualizado = await _supplierService.UpdateAsync(id, datos);
                return Ok(ApiResponse<Supplier>.Exito(actualizado, "Proveedor actualizado correctamente"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        // 5. ELIMINAR
        [HttpDelete("{id}")]
        [RequierePermiso("delete.suppliers")]
        public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
        {
            try
            {
                await _supplierService.DeleteAsync(id);
                return Ok(ApiResponse<object>.Exito(null, "Proveedor eliminado (desactivado) correctamente"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
    }
}