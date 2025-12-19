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

        public async Task<List<Supplier>> GetAllAsync()
        {
            // Solo mostramos los activos por defecto
            return await _context.Suppliers
                .AsNoTracking()
                .Where(s => s.IsActive)
                .ToListAsync();
        }

        public async Task<Supplier> GetByIdAsync(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) throw new Exception("Proveedor no encontrado.");
            return supplier;
        }

        // --- BÚSQUEDA POR NOMBRE ---
        public async Task<List<Supplier>> SearchAsync(string termino)
        {
            if (string.IsNullOrWhiteSpace(termino)) return new List<Supplier>();

            return await _context.Suppliers
                .AsNoTracking()
                .Where(s => s.IsActive && s.Name.Contains(termino)) // Busca coincidencias parciales
                .ToListAsync();
        }

        // --- CREAR (CON VALIDACIÓN DE DUPLICADOS) ---
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

        // --- EDITAR (TAMBIÉN VALIDA DUPLICADOS) ---
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