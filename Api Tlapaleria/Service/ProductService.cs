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
                var productoDuplicado = await _context.Products.FirstOrDefaultAsync(p => p.InternalCode == datos.InternalCode);

                if (productoDuplicado != null)
                {
                    if (!productoDuplicado.IsActive)
                        throw new Exception($"El código '{datos.InternalCode}' pertenece a un producto inactivo/eliminado. Por favor, busca el producto en la papelera y reactívalo en lugar de crear uno nuevo.");
                    else
                        throw new Exception($"El código '{datos.InternalCode}' ya está siendo usado por un producto activo.");
                }

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
        //Buscador ID Producto GET
        public async Task<Product> GetProductByIdAsync(int id, bool isActive = true)
        {
            // Buscamos el producto Padre y traemos a sus Hijos con .Include()
            var product = await _context.Products
                .Include(p => p.Presentations)
                .Include(p => p.Supplier) // Opcional: Traemos datos del proveedor por si los necesitas ver
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive == isActive); 

            if (product == null)
                throw new Exception($"El producto con ID {id} no fue encontrado.");

            return product;
        }
        //Buscador de Prodcuto(Name,Barcode,internalCode) GET
        public async Task<List<Product>> SearchProductsAsync(string? searchTerm, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Product>(); // Si mandan vacío, regresamos lista vacía

            var term = searchTerm.ToLower().Trim();

            var resultados = await _context.Products
                .Include(p => p.Presentations)
                .Where(p => p.IsActive == isActive && (
                    // Busca en el PADRE
                    p.Name.ToLower().Contains(term) ||
                    p.InternalCode.ToLower().Contains(term) ||
                    p.Barcode == term ||
                    // Busca en los HIJOS (Presentaciones)
                    p.Presentations.Any(pres => pres.Barcode == term || pres.Code == term)
                ))
                .ToListAsync();

            return resultados;
        }
        //Muestra de todos los productos mediante paginacion 
        public async Task<PagedResponse<Product>> GetAllProductsAsync(int pageNumber = 1, int pageSize = 50, bool isActive = true)
        {
            // 1. Armamos la consulta base (sin ejecutarla aún)
            var query = _context.Products
                .Include(p => p.Presentations)
                .Where(p => p.IsActive == isActive);

            // 2. Contamos el total real de registros en la base de datos
            var totalItems = await query.CountAsync();

            // 3. Calculamos el total de páginas
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // 4. Traemos SOLO los registros de la página solicitada
            var productos = await query
                .Skip((pageNumber - 1) * pageSize) // Si estoy en la pag 2 y el size es 50, salta los primeros 50
                .Take(pageSize)                    // Toma los siguientes 50
                .ToListAsync();                    // Aquí es donde realmente va a la BD

            // 5. Devolvemos el paquete completo
            return new PagedResponse<Product>
            {
                Data = productos,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };
        }
        //Actualizar Prodcutos usando reglas 
        public async Task<Product> UpdateProductAsync(int id, UpdateProductDto datos)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Buscamos el producto actual CON sus presentaciones
                var productoExistente = await _context.Products
                    .Include(p => p.Presentations)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (productoExistente == null)
                    throw new Exception("El producto no existe.");

                if (!productoExistente.IsActive)
                    throw new Exception("No puedes editar un producto que está desactivado/eliminado. Reactívalo primero.");

                // 2. REGLA: Validar que el InternalCode no se repita (excluyendo este mismo producto)
                bool existeInternalCode = await _context.Products
                    .AnyAsync(p => p.InternalCode == datos.InternalCode && p.Id != id);
                if (existeInternalCode)
                    throw new Exception($"El código interno '{datos.InternalCode}' ya está siendo usado por otro producto.");

                // 3. REGLA: Validar que el Barcode del Padre no se repita (si es que mandan uno)
                if (!string.IsNullOrWhiteSpace(datos.Barcode))
                {
                    bool existeBarcode = await _context.Products
                        .AnyAsync(p => p.Barcode == datos.Barcode && p.Id != id);
                    if (existeBarcode)
                        throw new Exception($"El código de barras '{datos.Barcode}' ya está registrado en otro producto.");
                }

                // 4. REGLA: Validar que el Proveedor exista
                bool existeProveedor = await _context.Suppliers
                    .AnyAsync(s => s.Id == datos.SupplierId && s.IsActive);
                if (!existeProveedor)
                    throw new Exception("El proveedor seleccionado no existe o está inactivo.");

                // --- ACTUALIZAMOS DATOS DEL PADRE ---
                productoExistente.InternalCode = datos.InternalCode;
                productoExistente.Barcode = datos.Barcode;
                productoExistente.Name = datos.Name;
                productoExistente.Description = datos.Description;
                productoExistente.Brand = datos.Brand;
                productoExistente.Location = datos.Location;
                productoExistente.SupplierId = datos.SupplierId;
                productoExistente.SupplierPrice = datos.SupplierPrice;
                productoExistente.ProfitMargin = datos.ProfitMargin;
                productoExistente.UnitOfMeasure = datos.UnitOfMeasure;
                // El stock NO se toca aquí.

                // --- MAGIA DE LOS HIJOS (Presentaciones) ---

                // A. Encontrar cuáles presentaciones eliminar (Están en BD pero no en el DTO)
                var idsEnDto = datos.Presentations.Where(p => p.Id.HasValue).Select(p => p.Id.Value).ToList();
                var presentacionesAEliminar = productoExistente.Presentations
                    .Where(p => !idsEnDto.Contains(p.Id))
                    .ToList();

                _context.ProductPresentations.RemoveRange(presentacionesAEliminar);

                // B. Actualizar las que ya existen y agregar las nuevas
                foreach (var presDto in datos.Presentations)
                {
                    if (presDto.Id.HasValue && presDto.Id.Value > 0)
                    {
                        // Actualizar existente
                        var presExistente = productoExistente.Presentations.FirstOrDefault(p => p.Id == presDto.Id.Value);
                        if (presExistente != null)
                        {
                            presExistente.Name = presDto.Name;
                            presExistente.Code = presDto.Code;
                            presExistente.Barcode = presDto.Barcode;
                            presExistente.Price = presDto.Price;
                            presExistente.StockFactor = presDto.StockFactor;
                        }
                    }
                    else
                    {
                        // Agregar nueva
                        productoExistente.Presentations.Add(new ProductPresentation
                        {
                            Name = presDto.Name,
                            Code = presDto.Code,
                            Barcode = presDto.Barcode,
                            Price = presDto.Price,
                            StockFactor = presDto.StockFactor,
                            IsActive = true
                        });
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return productoExistente;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        //Eliminar(Desactiavr) Productos 
        public async Task<bool> DeleteProductAsync(int id)
        {
            // Buscamos el producto con sus presentaciones
            var producto = await _context.Products
                .Include(p => p.Presentations)
                .FirstOrDefaultAsync(p => p.Id == id);

            // Validaciones
            if (producto == null)
                throw new Exception($"No se encontró ningún producto con el ID {id}.");

            if (!producto.IsActive)
                throw new Exception("Este producto ya se encuentra inactivo (eliminado).");

            // BORRADO LÓGICO: Apagamos el Padre
            producto.IsActive = false;

            // Apagamos a los Hijos para que ya no salgan en las búsquedas del mostrador
            foreach (var presentacion in producto.Presentations)
            {
                presentacion.IsActive = false;
            }

            // Guardamos los cambios
            await _context.SaveChangesAsync();

            return true;
        }
        //Reactivar un producto 
        public async Task<bool> ReactivateProductAsync(int id)
        {
            // Buscamos el producto con sus presentaciones
            var producto = await _context.Products
                .Include(p => p.Presentations)
                .FirstOrDefaultAsync(p => p.Id == id);

            // Validaciones
            if (producto == null)
                throw new Exception($"No se encontró ningún producto con el ID {id}.");

            if (producto.IsActive)
                throw new Exception("Este producto ya está activo en el sistema.");

            // REACTIVACIÓN: Encendemos al Padre
            producto.IsActive = true;

            // Encendemos a los Hijos para que vuelvan a salir en mostrador
            foreach (var presentacion in producto.Presentations)
            {
                presentacion.IsActive = true;
            }

            // Guardamos los cambios
            await _context.SaveChangesAsync();

            return true;
        }
    }
}