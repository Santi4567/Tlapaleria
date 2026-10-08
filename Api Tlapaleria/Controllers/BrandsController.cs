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
    public class BrandsController : ControllerBase
    {
        private readonly IBrandService _brandService;

        public BrandsController(IBrandService brandService)
        {
            _brandService = brandService;
        }

        // GET api/brands  -> catálogo completo (el front lo carga una vez y filtra en React)
        [HttpGet]
        [RequierePermiso("view.products")]
        public async Task<ActionResult<ApiResponse<List<BrandDto>>>> GetAll()
        {
            var marcas = await _brandService.GetAllAsync();
            return Ok(ApiResponse<List<BrandDto>>.Exito(marcas));
        }

        // GET api/brands/search?q=3m  -> sugerencias desde la primera tecla
        [HttpGet("search")]
        [RequierePermiso("view.products")]
        public async Task<ActionResult<ApiResponse<List<BrandDto>>>> Search([FromQuery] string? q = "")
        {
            var marcas = await _brandService.SearchAsync(q ?? "");
            return Ok(ApiResponse<List<BrandDto>>.Exito(marcas));
        }

        // PUT api/brands/5  -> corregir el nombre (Trupper -> Truper)
        [HttpPut("{id:int}")]
        [RequierePermiso("edit.products")]
        public async Task<ActionResult<ApiResponse<BrandDto>>> Rename(int id, [FromBody] RenameBrandDto datos)
        {
            var marca = await _brandService.RenameAsync(id, datos.Name);
            return Ok(ApiResponse<BrandDto>.Exito(marca, "Marca renombrada correctamente"));
        }

        // PUT api/brands/merge  -> migrar productos de una marca a otra y eliminar la origen
        [HttpPut("merge")]
        [RequierePermiso("edit.products")]
        public async Task<ActionResult<ApiResponse<int>>> Merge([FromBody] MergeBrandsDto datos)
        {
            var migrados = await _brandService.MergeAsync(datos.SourceBrandId, datos.TargetBrandId);
            return Ok(ApiResponse<int>.Exito(migrados, $"{migrados} producto(s) migrados correctamente"));
        }

        // DELETE api/brands/5  -> "Eliminar recomendación" (solo si ningún producto la usa)
        [HttpDelete("{id:int}")]
        [RequierePermiso("delete.products")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            await _brandService.DeleteAsync(id);
            return Ok(ApiResponse<bool>.Exito(true, "Recomendación eliminada"));
        }
    }
}
