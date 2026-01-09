using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class ProductService : IProductService
    {
        private readonly TlapaleriaContext _context;

        public ProductService(TlapaleriaContext context)
        {
            _context = context;
        }

        public async Task<Product> CreateProductAsync(CreateProductDto datos)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. VALIDAR DUPLICADOS (Código Interno)
                bool existeCodigo = await _context.Products
                    .AnyAsync(p => p.InternalCode == datos.InternalCode);

                if (existeCodigo)
                    throw new Exception($"El código '{datos.InternalCode}' ya existe.");

                // 2. VALIDAR QUE EL PROVEEDOR EXISTA (--- NUEVO ---)
                bool existeProveedor = await _context.Suppliers
                    .AnyAsync(s => s.Id == datos.SupplierId && s.IsActive); // Opcional: validar que esté activo

                if (!existeProveedor)
                {
                    throw new Exception($"El proveedor seleccionado (ID {datos.SupplierId}) no existe o está inactivo.");
                }
                // ----------------------------------------------------

                // 3. CREAR AL PADRE
                var nuevoProducto = new Product
                {
                    InternalCode = datos.InternalCode,
                    Barcode = datos.Barcode,
                    Name = datos.Name,
                    Description = datos.Description,
                    Brand = datos.Brand,
                    Location = datos.Location,
                    SupplierId = datos.SupplierId,
                    SupplierPrice = datos.SupplierPrice,
                    ProfitMargin = datos.ProfitMargin,
                    UnitOfMeasure = datos.UnitOfMeasure,
                    CurrentStock = datos.InitialStock,
                    IsActive = true
                };

                _context.Products.Add(nuevoProducto);
                await _context.SaveChangesAsync();

                // 4. CREAR LOS HIJOS (Presentaciones)
                foreach (var presDto in datos.Presentations)
                {
                    var nuevaPresentacion = new ProductPresentation
                    {
                        ProductId = nuevoProducto.Id,
                        Name = presDto.Name,
                        Code = presDto.Code,
                        Barcode = presDto.Barcode,
                        Price = presDto.Price,
                        StockFactor = presDto.StockFactor,
                        IsActive = true
                    };
                    _context.ProductPresentations.Add(nuevaPresentacion);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return nuevoProducto;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}