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

        //POST: Crear un nuevo registro en la tabla de Productos
        public async Task<Product> CreateProductAsync(CreateProductDto datos, int userIdToken)
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

                // 2. VALIDAR QUE EL PROVEEDOR EXISTA 
                bool existeProveedor = await _context.Suppliers
                    .AnyAsync(s => s.Id == datos.SupplierId && s.IsActive);

                if (!existeProveedor)
                {
                    throw new Exception($"El proveedor seleccionado (ID {datos.SupplierId}) no existe o está inactivo.");
                }

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
                    AllowFractions = datos.AllowFractions,
                    CurrentStock = datos.InitialStock,
                    IsInventoryTracked = datos.IsInventoryTracked,
                    HasExpiration = datos.HasExpiration,
                    NextExpirationDate = datos.NextExpirationDate,
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
                // <-- AQUÍ estaba el "await transaction.CommitAsync();" que sobraba. Se quitó.
                //     La transacción se queda ABIERTA hasta que también se guarde el historial.

                // 5. Historial del Costo del Proveedor
                var historialCosto = new ProductSupplierPriceHistory
                {
                    ProductId = nuevoProducto.Id,
                    OldSupplierPrice = 0,
                    NewSupplierPrice = datos.SupplierPrice,
                    UserId = userIdToken
                };
                _context.ProductSupplierPriceHistories.Add(historialCosto);

                // 6. Historial de Precios por CADA presentación creada
                // nuevoProducto.Presentations ya trae los Ids porque EF los generó en el SaveChangesAsync de arriba
                foreach (var presNueva in nuevoProducto.Presentations)
                {
                    var historialPrecio = new PresentationPriceHistory
                    {
                        PresentationId = presNueva.Id,
                        ProductId = nuevoProducto.Id,
                        OldPrice = 0,
                        NewPrice = presNueva.Price,
                        UserId = userIdToken
                    };
                    _context.PresentationPriceHistories.Add(historialPrecio);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync(); // <-- único commit, hasta el final

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
            var product = await _context.Products
                .Include(p => p.Presentations)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive == isActive);

            if (product == null)
                throw new Exception($"El producto con ID {id} no fue encontrado.");

            return product;
        }

        //Buscador de Prodcuto(Name,Barcode,internalCode) GET
        public async Task<List<Product>> SearchProductsAsync(string? searchTerm, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Product>();

            var term = searchTerm.ToLower().Trim();

            var resultados = await _context.Products
                .AsNoTracking()
                .Include(p => p.Presentations)
                .Where(p => p.IsActive == isActive && (
                    p.Name.ToLower().Contains(term) ||
                    p.InternalCode.ToLower().Contains(term) ||
                    p.Barcode == term ||
                    p.Presentations.Any(pres => pres.Barcode == term || pres.Code == term)
                ))
                .OrderBy(p => p.Name)
                .Take(10)
                .ToListAsync();

            return resultados;
        }

        //Muestra de todos los productos mediante paginacion 
        public async Task<PagedResponse<Product>> GetAllProductsAsync(int pageNumber = 1, int pageSize = 50, bool isActive = true)
        {
            var query = _context.Products
                .Include(p => p.Presentations)
                .Where(p => p.IsActive == isActive);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var productos = await query
                .OrderBy(p => p.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResponse<Product>
            {
                Data = productos,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };
        }

        //Actualizar Prodcutos usando reglas 
        public async Task<Product> UpdateProductAsync(int id, UpdateProductDto datos, int userIdToken)
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

                // 2. REGLA: InternalCode no repetido
                bool existeInternalCode = await _context.Products
                    .AnyAsync(p => p.InternalCode == datos.InternalCode && p.Id != id);
                if (existeInternalCode)
                    throw new Exception($"El código interno '{datos.InternalCode}' ya está siendo usado por otro producto.");

                // 3. REGLA: Barcode del padre no repetido
                if (!string.IsNullOrWhiteSpace(datos.Barcode))
                {
                    bool existeBarcode = await _context.Products
                        .AnyAsync(p => p.Barcode == datos.Barcode && p.Id != id);
                    if (existeBarcode)
                        throw new Exception($"El código de barras '{datos.Barcode}' ya está registrado en otro producto.");
                }

                // 4. REGLA: Proveedor válido
                bool existeProveedor = await _context.Suppliers
                    .AnyAsync(s => s.Id == datos.SupplierId && s.IsActive);
                if (!existeProveedor)
                    throw new Exception("El proveedor seleccionado no existe o está inactivo.");

                // --- CAPTURAMOS EL COSTO VIEJO ANTES DE PISARLO ---
                decimal costoAnterior = productoExistente.SupplierPrice;

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
                productoExistente.AllowFractions = datos.AllowFractions;
                productoExistente.IsInventoryTracked = datos.IsInventoryTracked;
                productoExistente.HasExpiration = datos.HasExpiration;
                productoExistente.NextExpirationDate = datos.NextExpirationDate;
                // El stock NO se toca aquí.

                // --- SI CAMBIÓ EL COSTO, GUARDAMOS SU HISTORIAL ---
                if (costoAnterior != datos.SupplierPrice)
                {
                    _context.ProductSupplierPriceHistories.Add(new ProductSupplierPriceHistory
                    {
                        ProductId = productoExistente.Id,
                        OldSupplierPrice = costoAnterior,
                        NewSupplierPrice = datos.SupplierPrice,
                        UserId = userIdToken
                    });
                }

                // --- MAGIA DE LOS HIJOS (Presentaciones) ---

                // A. Eliminar las que están en BD pero no en el DTO
                var idsEnDto = datos.Presentations.Where(p => p.Id.HasValue).Select(p => p.Id.Value).ToList();
                var presentacionesAEliminar = productoExistente.Presentations
                    .Where(p => !idsEnDto.Contains(p.Id))
                    .ToList();

                _context.ProductPresentations.RemoveRange(presentacionesAEliminar);

                // B. Actualizar existentes y preparar las nuevas
                var presentacionesNuevas = new List<ProductPresentation>();

                foreach (var presDto in datos.Presentations)
                {
                    if (presDto.Id.HasValue && presDto.Id.Value > 0)
                    {
                        var presExistente = productoExistente.Presentations.FirstOrDefault(p => p.Id == presDto.Id.Value);
                        if (presExistente != null)
                        {
                            decimal precioAnterior = presExistente.Price;

                            presExistente.Name = presDto.Name;
                            presExistente.Code = presDto.Code;
                            presExistente.Barcode = presDto.Barcode;
                            presExistente.Price = presDto.Price;
                            presExistente.StockFactor = presDto.StockFactor;

                            // Si cambió el precio público de ESTA presentación, va su propio historial
                            if (precioAnterior != presDto.Price)
                            {
                                _context.PresentationPriceHistories.Add(new PresentationPriceHistory
                                {
                                    PresentationId = presExistente.Id,
                                    ProductId = productoExistente.Id,
                                    OldPrice = precioAnterior,
                                    NewPrice = presDto.Price,
                                    UserId = userIdToken
                                });
                            }
                        }
                    }
                    else
                    {
                        var presentacionNueva = new ProductPresentation
                        {
                            Name = presDto.Name,
                            Code = presDto.Code,
                            Barcode = presDto.Barcode,
                            Price = presDto.Price,
                            StockFactor = presDto.StockFactor,
                            IsActive = true
                        };
                        productoExistente.Presentations.Add(presentacionNueva);
                        presentacionesNuevas.Add(presentacionNueva); // guardamos la referencia; aún no tiene Id
                    }
                }

                await _context.SaveChangesAsync(); // EF le asigna Id a cada presentación nueva aquí

                // C. Ahora sí, historial "de nacimiento" para las presentaciones nuevas (0 -> precio inicial)
                foreach (var nueva in presentacionesNuevas)
                {
                    _context.PresentationPriceHistories.Add(new PresentationPriceHistory
                    {
                        PresentationId = nueva.Id, // ya viene poblado por EF
                        ProductId = productoExistente.Id,
                        OldPrice = 0,
                        NewPrice = nueva.Price,
                        UserId = userIdToken
                    });
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
            var producto = await _context.Products
                .Include(p => p.Presentations)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (producto == null)
                throw new Exception($"No se encontró ningún producto con el ID {id}.");

            if (!producto.IsActive)
                throw new Exception("Este producto ya se encuentra inactivo (eliminado).");

            producto.IsActive = false;

            foreach (var presentacion in producto.Presentations)
            {
                presentacion.IsActive = false;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        //Reactivar un producto 
        public async Task<bool> ReactivateProductAsync(int id)
        {
            var producto = await _context.Products
                .Include(p => p.Presentations)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (producto == null)
                throw new Exception($"No se encontró ningún producto con el ID {id}.");

            if (producto.IsActive)
                throw new Exception("Este producto ya está activo en el sistema.");

            producto.IsActive = true;

            foreach (var presentacion in producto.Presentations)
            {
                presentacion.IsActive = true;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        //Alerta de caduccidad de los productos 
        public async Task<List<ExpiringProductDto>> GetExpiringProductsAsync()
        {
            DateTime limiteAlerta = DateTime.Now.AddDays(30);

            var productosEnRiesgo = await _context.Products
                .Where(p => p.IsActive
                         && p.HasExpiration
                         && p.NextExpirationDate != null
                         && p.NextExpirationDate <= limiteAlerta)
                .Select(p => new ExpiringProductDto
                {
                    Id = p.Id,
                    InternalCode = p.InternalCode,
                    Name = p.Name,
                    NextExpirationDate = p.NextExpirationDate,
                    DaysRemaining = (p.NextExpirationDate.Value - DateTime.Now).Days
                })
                .OrderBy(p => p.NextExpirationDate)
                .ToListAsync();

            return productosEnRiesgo;
        }

        // Verificar si un código interno ya está siendo utilizado
        public async Task<string?> CheckInternalCodeAsync(string internalCode)
        {
            if (string.IsNullOrWhiteSpace(internalCode))
                return null;

            var term = internalCode.Trim().ToLower();

            var nombreProducto = await _context.Products
                .AsNoTracking()
                .Where(p => p.InternalCode.ToLower() == term)
                .Select(p => p.Name)
                .FirstOrDefaultAsync();

            return nombreProducto;
        }
    }
}