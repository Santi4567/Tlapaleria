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
    [Authorize] // 1. Candado General: Solo usuarios logueados entran
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        // POST: api/products
        [HttpPost]
        [RequierePermiso("add.products")] // 2. Candado Específico: Solo quien tenga este permiso
        public async Task<ActionResult<ApiResponse<Product>>> Create([FromBody] CreateProductDto datos)
        {
            try
            {
                // La magia ocurre aquí: El servicio crea Padre + Hijos en una transacción
                var productoCreado = await _productService.CreateProductAsync(datos);

                return Ok(ApiResponse<Product>.Exito(productoCreado, "Producto y presentaciones registrados correctamente"));
            }
            catch (Exception ex)
            {
                // Si falló la validación, el código duplicado o la transacción, cae aquí
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
        // GET: api/products/{id}?isActive=false
        [HttpGet("{id}")]
        [RequierePermiso("view.products")]
        public async Task<ActionResult<ApiResponse<Product>>> GetById(
            int id,
            [FromQuery] bool isActive = true) // <-- RECIBE EL PARÁMETRO
        {
            try
            {
                var producto = await _productService.GetProductByIdAsync(id, isActive);
                return Ok(ApiResponse<Product>.Exito(producto));
            }
            catch (Exception ex)
            {
                return NotFound(ApiResponse<object>.Error(ex.Message));
            }
        }

        // GET: api/products?page=1&pageSize=50&isActive=true
        [HttpGet]
        [RequierePermiso("view.products")]
        public async Task<ActionResult<ApiResponse<PagedResponse<Product>>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] bool isActive = true) // <-- Recibe el estado 
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 100) pageSize = 100;

                var resultadoPaginado = await _productService.GetAllProductsAsync(page, pageSize, isActive);
                return Ok(ApiResponse<PagedResponse<Product>>.Exito(resultadoPaginado));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        // GET: api/products/search?query=clavos&isActive=false
        [HttpGet("search")]
        [RequierePermiso("view.products")]
        public async Task<ActionResult<ApiResponse<List<Product>>>> Search(
            [FromQuery] string? query = "",
            [FromQuery] bool isActive = true) // <-- Recibe el estado 
        {
            try
            {
                var productos = await _productService.SearchProductsAsync(query, isActive);
                return Ok(ApiResponse<List<Product>>.Exito(productos));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
        // PUT: api/products/{id}
        [HttpPut("{id}")]
        [RequierePermiso("edit.products")] // Ojo al permiso
        public async Task<ActionResult<ApiResponse<Product>>> Update(int id, [FromBody] UpdateProductDto datos)
        {
            try
            {
                var productoActualizado = await _productService.UpdateProductAsync(id, datos);
                return Ok(ApiResponse<Product>.Exito(productoActualizado, "Producto actualizado correctamente"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
        // DELETE: api/products/{id}
        [HttpDelete("{id}")]
        [RequierePermiso("delete.products")] // Validamos que tenga permiso de borrar
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            try
            {
                var fueEliminado = await _productService.DeleteProductAsync(id);

                return Ok(ApiResponse<bool>.Exito(fueEliminado, "Producto eliminado (desactivado) exitosamente."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
        // PUT: api/products/{id}/reactivate
        [HttpPut("{id}/reactivate")]
        [RequierePermiso("edit.products")] // Puedes usar el de edición o crear uno como "restore.products"
        public async Task<ActionResult<ApiResponse<bool>>> Reactivate(int id)
        {
            try
            {
                var fueReactivado = await _productService.ReactivateProductAsync(id);

                return Ok(ApiResponse<bool>.Exito(fueReactivado, "Producto restaurado y listo para la venta."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
    }
}