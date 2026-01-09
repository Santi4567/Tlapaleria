using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IProductService
    {
        Task<Product> CreateProductAsync(CreateProductDto datos);
        // Aquí agregaremos luego Search, Update, etc.
    }
}