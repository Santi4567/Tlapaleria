using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface ISupplierService
    {
        Task<List<Supplier>> GetAllAsync();
        Task<Supplier> GetByIdAsync(int id);
        Task<List<Supplier>> SearchAsync(string termino); // <--- TU REQUISITO DE BÚSQUEDA

        Task<Supplier> CreateAsync(CreateSupplierDto datos);
        Task<Supplier> UpdateAsync(int id, UpdateSupplierDto datos);
        Task<bool> DeleteAsync(int id); // Borrado lógico
    }
}