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
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpPost]
        [RequierePermiso("add.products")]
        public async Task<ActionResult<ApiResponse<Product>>> Create([FromBody] CreateProductDto datos)
        {
            var productoCreado = await _productService.CreateProductAsync(datos);
            return Ok(ApiResponse<Product>.Exito(productoCreado, "Producto y presentaciones registrados correctamente"));
        }

        [HttpGet("{id}")]
        [RequierePermiso("view.products")]
        public async Task<ActionResult<ApiResponse<Product>>> GetById(
            int id,
            [FromQuery] bool isActive = true)
        {
            // El Middleware atrapará el error si el ID no existe y lo convertirá a NotFound (404)
            var producto = await _productService.GetProductByIdAsync(id, isActive);
            return Ok(ApiResponse<Product>.Exito(producto));
        }

        [HttpGet]
        [RequierePermiso("view.products")]
        public async Task<ActionResult<ApiResponse<PagedResponse<Product>>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] bool isActive = true)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var resultadoPaginado = await _productService.GetAllProductsAsync(page, pageSize, isActive);
            return Ok(ApiResponse<PagedResponse<Product>>.Exito(resultadoPaginado));
        }

        [HttpGet("search")]
        [RequierePermiso("view.products")]
        public async Task<ActionResult<ApiResponse<List<Product>>>> Search(
            [FromQuery] string? query = "",
            [FromQuery] bool isActive = true)
        {
            var productos = await _productService.SearchProductsAsync(query, isActive);
            return Ok(ApiResponse<List<Product>>.Exito(productos));
        }

        [HttpPut("{id}")]
        [RequierePermiso("edit.products")]
        public async Task<ActionResult<ApiResponse<Product>>> Update(int id, [FromBody] UpdateProductDto datos)
        {
            var productoActualizado = await _productService.UpdateProductAsync(id, datos);
            return Ok(ApiResponse<Product>.Exito(productoActualizado, "Producto actualizado correctamente"));
        }

        [HttpDelete("{id}")]
        [RequierePermiso("delete.products")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var fueEliminado = await _productService.DeleteProductAsync(id);
            return Ok(ApiResponse<bool>.Exito(fueEliminado, "Producto eliminado (desactivado) exitosamente."));
        }

        [HttpPut("{id}/reactivate")]
        [RequierePermiso("edit.products")]
        public async Task<ActionResult<ApiResponse<bool>>> Reactivate(int id)
        {
            var fueReactivado = await _productService.ReactivateProductAsync(id);
            return Ok(ApiResponse<bool>.Exito(fueReactivado, "Producto restaurado y listo para la venta."));
        }

        [HttpGet("alerts/expiring-soon")]
        public async Task<IActionResult> GetExpiringProducts()
        {
            var productos = await _productService.GetExpiringProductsAsync();
            return Ok(productos);
        }

        [HttpGet("check-internal-code")]
        [RequierePermiso("view.products")]
        public async Task<ActionResult<ApiResponse<object>>> CheckInternalCode([FromQuery] string code)
        {
            var nombreProducto = await _productService.CheckInternalCodeAsync(code);

            if (nombreProducto == null)
            {
                return Ok(ApiResponse<object>.Exito(
                    new { existe = false, nombreProducto = (string?)null },
                    "No se encontraron coincidencias"
                ));
            }

            return Ok(ApiResponse<object>.Exito(
                new { existe = true, nombreProducto = nombreProducto },
                "El codigo interno ya esta usado "
            ));
        }
    }
}