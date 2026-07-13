using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class SupplierService : ISupplierService
    {
        private readonly TlapaleriaContext _context;

        public SupplierService(TlapaleriaContext context)
        {
            _context = context;
        }

        // GET: Mostrar proveedores paginados 
        public async Task<PagedResponse<Supplier>> GetAllAsync(bool isActive = true, int pageNumber = 1, int pageSize = 10)
        {
            // 1. Seguridad básica en los parámetros
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            // 2. Armamos la consulta base
            var query = _context.Suppliers
                .AsNoTracking()
                .Where(s => s.IsActive == isActive);

            // 3. Contamos cuántos proveedores hay en total en la BD
            var totalItems = await query.CountAsync();

            // 4. Traemos solo la "rebanada" de la página actual (LIMIT y OFFSET en MariaDB)
            var items = await query
                .OrderBy(s => s.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 5. Calculamos el total de páginas y llenamos TU objeto DTO
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            return new PagedResponse<Supplier>
            {
                Data = items,             // Tu lista de proveedores de esta página
                TotalItems = totalItems,  // Total en la base de datos
                TotalPages = totalPages,  // Total de páginas calculadas
                CurrentPage = pageNumber  // Página en la que estamos
            };
        }

        //GET: Traer a un proveedor por ID 
        public async Task<Supplier> GetByIdAsync(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) throw new Exception("Proveedor no encontrado.");
            return supplier;
        }

        // GET: BÚSQUEDA POR NOMBRE CON FILTRO DE ESTADO
        public async Task<List<Supplier>> SearchAsync(string termino, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(termino)) return new List<Supplier>();

            return await _context.Suppliers
                .AsNoTracking()
                .Where(s => s.IsActive == isActive && s.Name.Contains(termino)) // <-- Ahora busca en activos O inactivos
                .Take(10) // Límite de seguridad intocable
                .ToListAsync();
        }

        //POST: CREAR un nuevo proveedor  
        public async Task<Supplier> CreateAsync(CreateSupplierDto datos)
        {
            // Validar si ya existe una empresa con ese nombre EXACTO
            bool existe = await _context.Suppliers
                .AnyAsync(s => s.Name == datos.Name);

            if (existe)
            {
                throw new Exception($"El proveedor '{datos.Name}' ya está registrado.");
            }

            var nuevo = new Supplier
            {
                Name = datos.Name,
                ContactName = datos.ContactName,
                Phone = datos.Phone,
                IsActive = true
            };

            _context.Suppliers.Add(nuevo);
            await _context.SaveChangesAsync();

            return nuevo;
        }

        // Post: EDITAR un proveedor 
        //Verifica que no existan duplicados 
        public async Task<Supplier> UpdateAsync(int id, UpdateSupplierDto datos)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) throw new Exception("Proveedor no encontrado.");

            // Validar que no le cambiemos el nombre al de OTRO proveedor existente
            // "Existe algún otro (Id != id) que se llame igual?"
            bool nombreOcupado = await _context.Suppliers
                .AnyAsync(s => s.Name == datos.Name && s.Id != id);

            if (nombreOcupado)
            {
                throw new Exception($"Ya existe otro proveedor llamado '{datos.Name}'.");
            }

            supplier.Name = datos.Name;
            supplier.ContactName = datos.ContactName;
            supplier.Phone = datos.Phone;
            supplier.IsActive = datos.IsActive;

            _context.Suppliers.Update(supplier);
            await _context.SaveChangesAsync();

            return supplier;
        }
        
        //DELETE: Desactiva provedores 
        //No se puede eliminar por integridad de la tabla Finanzas 
        public async Task<bool> DeleteAsync(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) throw new Exception("Proveedor no encontrado.");

            // Borrado Lógico
            supplier.IsActive = false;

            _context.Suppliers.Update(supplier);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}