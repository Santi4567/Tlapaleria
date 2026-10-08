using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IBrandService
    {
        // Busca la marca por nombre; si no existe la crea. Lo usa ProductService en alta y edición
        Task<Brand> GetOrCreateAsync(string name);

        // Sugerencias por prefijo (funciona desde la primera letra: "3M")
        Task<List<BrandDto>> SearchAsync(string q);

        // Todas las marcas activas (el catálogo es chico: el front puede cargarlo una vez)
        Task<List<BrandDto>> GetAllAsync();

        // Corrige el nombre de una marca y de todos los productos que la usan
        Task<BrandDto> RenameAsync(int id, string newName);

        // Migra los productos de la marca origen a la destino y elimina la origen. Devuelve cuántos migró
        Task<int> MergeAsync(int sourceId, int targetId);

        // Elimina una marca solo si ningún producto la usa
        Task DeleteAsync(int id);
    }
}
