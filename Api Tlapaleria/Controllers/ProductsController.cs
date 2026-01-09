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
    }
}