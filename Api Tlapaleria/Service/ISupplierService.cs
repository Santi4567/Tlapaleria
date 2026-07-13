using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface ISupplierService
    {
        //Muestra todos los preveedores con paginacion 
        Task<PagedResponse<Supplier>> GetAllAsync(bool isActive = true, int pageNumber = 1, int pageSize = 10);
        //Muestra Informacion del Proveedor por ID
        Task<Supplier> GetByIdAsync(int id);
        //Buscador dinamico por coincidencias 
        Task<List<Supplier>> SearchAsync(string termino, bool isActive = true);
        //Crea un nuevo registro en la Tabal de Proveedores
        Task<Supplier> CreateAsync(CreateSupplierDto datos);
        //Actualiza un proveedor 
        Task<Supplier> UpdateAsync(int id, UpdateSupplierDto datos);
        //Desactiva a un proveedor 
        Task<bool> DeleteAsync(int id); // Borrado lógico
    }
}