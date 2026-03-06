using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IProductService
    {
        //Creacion de Productos
        Task<Product> CreateProductAsync(CreateProductDto datos);
        //Buscador por ID Producto
        Task<Product> GetProductByIdAsync(int id, bool isActive = true);
        //Buscador de Prodcuto(Name,Barcode,internalCode)
        Task<List<Product>> SearchProductsAsync(string searchTerm, bool isActive = true);
        //Mostrar los productos en paginacion 
        Task<PagedResponse<Product>> GetAllProductsAsync(int pageNumber = 1, int pageSize = 50, bool isActive = true);
        //Actualizar los productos 
        Task<Product> UpdateProductAsync(int id, UpdateProductDto datos);
        //Eliminar(Desactivar) Producto
        Task<bool> DeleteProductAsync(int id);
        //Reactivar un producto 
        Task<bool> ReactivateProductAsync(int id);
    }
}