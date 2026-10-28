using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Api_Tlapaleria.Services
{
    public class ProductService : IProductService
    {
        private readonly TlapaleriaContext _context;
        private readonly IBrandService _brandService;

        public ProductService(TlapaleriaContext context, IBrandService brandService)
        {
            _context = context;
            _brandService = brandService;
        }

        //POST: Crear un nuevo registro en la tabla de Productos
        public async Task<Product> CreateProductAsync(CreateProductDto datos, int userIdToken)
        {
            // Validacion de Marca: resolver la marca ANTES de abrir la transacción
            string? brandName = null;
            if (!string.IsNullOrWhiteSpace(datos.Brand))
                brandName = (await _brandService.GetOrCreateAsync(datos.Brand)).Name;

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
                    Brand = brandName,
                    Location = datos.Location,
                    SupplierId = datos.SupplierId,
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
                        SupplierPrice = presDto.SupplierPrice,
                        Price = presDto.Price,
                        StockFactor = presDto.StockFactor,
                        IsActive = true
                    };
                    _context.ProductPresentations.Add(nuevaPresentacion);
                }

                await _context.SaveChangesAsync();
                // <-- AQUÍ estaba el "await transaction.CommitAsync();" que sobraba. Se quitó.
                //     La transacción se queda ABIERTA hasta que también se guarde el historial.

                // 5. Historial de Precio y Costo por CADA presentación creada
                // nuevoProducto.Presentations ya trae los Ids porque EF los generó en el SaveChangesAsync de arriba
                foreach (var presNueva in nuevoProducto.Presentations)
                {
                    _context.PresentationPriceHistories.Add(new PresentationPriceHistory
                    {
                        PresentationId = presNueva.Id,
                        ProductId = nuevoProducto.Id,
                        OldPrice = 0,
                        NewPrice = presNueva.Price,
                        UserId = userIdToken
                    });

                    _context.PresentationSupplierPriceHistories.Add(new PresentationSupplierPriceHistory
                    {
                        PresentationId = presNueva.Id,
                        ProductId = nuevoProducto.Id,
                        OldSupplierPrice = 0,
                        NewSupplierPrice = presNueva.SupplierPrice,
                        UserId = userIdToken
                    });
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

        // Buscador inteligente de Producto (Multi-palabra, Nombre, Marca, Barcode, InternalCode y Presentaciones)
        public async Task<List<Product>> SearchProductsAsync(string? searchTerm, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Product>();

            var cleanTerm = searchTerm.Trim().ToLower();

            // 1. Separamos por espacios para permitir buscar palabras salteadas (ej. "punta ph2 truper")
            var words = cleanTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Presentations)
                .Where(p => p.IsActive == isActive);

            // 2. Cada palabra escrita debe coincidir en al menos uno de los campos del producto o sus hijos
            foreach (var word in words)
            {
                var w = word; // Variable local para el closure de EF Core
                query = query.Where(p =>
                    p.Name.ToLower().Contains(w) ||
                    p.InternalCode.ToLower().Contains(w) ||
                    (p.Brand != null && p.Brand.ToLower().Contains(w)) ||
                    (p.Description != null && p.Description.ToLower().Contains(w)) ||
                    (p.Barcode != null && p.Barcode.ToLower().Contains(w)) ||
                    p.Presentations.Any(pres =>
                        (pres.Barcode != null && pres.Barcode.ToLower().Contains(w)) ||
                        (pres.Code != null && pres.Code.ToLower().Contains(w)) ||
                        pres.Name.ToLower().Contains(w)
                    )
                );
            }

            // 3. Ordenamos dando prioridad a coincidencias exactas de código/barcode o nombres que empiecen con lo buscado
            var resultados = await query
                .OrderByDescending(p => p.Barcode == cleanTerm || p.InternalCode.ToLower() == cleanTerm)
                .ThenByDescending(p => p.Name.ToLower().StartsWith(cleanTerm))
                .ThenBy(p => p.Name)
                .Take(20) // Subimos de 10 a 20 para que tengas mejor visibilidad en familias grandes de tornillería/herramienta
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
            // Validacion de Marca: resolver la marca ANTES de abrir la transacción
            string? brandName = null;
            if (!string.IsNullOrWhiteSpace(datos.Brand))
                brandName = (await _brandService.GetOrCreateAsync(datos.Brand)).Name;

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

                // --- ACTUALIZAMOS DATOS DEL PADRE ---
                productoExistente.InternalCode = datos.InternalCode;
                productoExistente.Barcode = datos.Barcode;
                productoExistente.Name = datos.Name;
                productoExistente.Description = datos.Description;
                productoExistente.Brand = brandName;
                productoExistente.Location = datos.Location;
                productoExistente.SupplierId = datos.SupplierId;
                productoExistente.ProfitMargin = datos.ProfitMargin;
                productoExistente.UnitOfMeasure = datos.UnitOfMeasure;
                productoExistente.AllowFractions = datos.AllowFractions;
                productoExistente.IsInventoryTracked = datos.IsInventoryTracked;
                productoExistente.HasExpiration = datos.HasExpiration;
                productoExistente.NextExpirationDate = datos.NextExpirationDate;
                // El stock NO se toca aquí.

                // --- MAGIA DE LOS HIJOS (Presentaciones) ---ls


                if (datos.Presentations == null || !datos.Presentations.Any())
                    throw new Exception("El producto debe tener al menos una presentación registrada.");

                // Validación: Si está activa para venta al público, debe tener precio > 0
                foreach (var p in datos.Presentations)
                {
                    if (p.IsActive && p.Price <= 0)
                        throw new Exception($"La presentación '{p.Name}' está activa para venta al público y debe tener un precio mayor a $0.");
                }

                // A. Si alguna presentación que existía en BD de plano se quitó del arreglo, pasa a inactiva
                var idsEnDto = datos.Presentations.Where(p => p.Id.HasValue && p.Id.Value > 0).Select(p => p.Id.Value).ToList();
                var presentacionesNoEnviadas = productoExistente.Presentations
                    .Where(p => !idsEnDto.Contains(p.Id) && p.IsActive)
                    .ToList();

                foreach (var presentacion in presentacionesNoEnviadas)
                {
                    presentacion.IsActive = false;
                }

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
                            decimal costoAnterior = presExistente.SupplierPrice;

                            presExistente.Name = presDto.Name;
                            presExistente.Code = presDto.Code;
                            presExistente.Barcode = presDto.Barcode;

                            // Si está inactiva (no venta al público) y mandan 0, pero ya tenía un precio antes, 
                            // puedes conservar el que tenía o dejar el que manden:
                            presExistente.Price = (!presDto.IsActive && presDto.Price == 0 && precioAnterior > 0)
                                ? precioAnterior
                                : presDto.Price;

                            presExistente.SupplierPrice = presDto.SupplierPrice;
                            presExistente.StockFactor = presDto.StockFactor;
                            presExistente.IsActive = presDto.IsActive; // <-- AHORA RESPETA EL SWITCH DEL FRONT

                            // Solo guardamos historial de precio público si realmente cambió y es mayor a 0
                            if (precioAnterior != presExistente.Price && presExistente.Price > 0)
                            {
                                _context.PresentationPriceHistories.Add(new PresentationPriceHistory
                                {
                                    PresentationId = presExistente.Id,
                                    ProductId = productoExistente.Id,
                                    OldPrice = precioAnterior,
                                    NewPrice = presExistente.Price,
                                    UserId = userIdToken
                                });
                            }

                            if (costoAnterior != presDto.SupplierPrice)
                            {
                                _context.PresentationSupplierPriceHistories.Add(new PresentationSupplierPriceHistory
                                {
                                    PresentationId = presExistente.Id,
                                    ProductId = productoExistente.Id,
                                    OldSupplierPrice = costoAnterior,
                                    NewSupplierPrice = presDto.SupplierPrice,
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
                            SupplierPrice = presDto.SupplierPrice,
                            Price = presDto.Price,
                            StockFactor = presDto.StockFactor,
                            IsActive = presDto.IsActive // <-- También permite crear el Bulto/Caja directamente como no-venta
                        };
                        productoExistente.Presentations.Add(presentacionNueva);
                        presentacionesNuevas.Add(presentacionNueva);
                    }
                }

                await _context.SaveChangesAsync();

                // C. Historial de nacimiento para las presentaciones nuevas
                foreach (var nueva in presentacionesNuevas)
                {
                    if (nueva.Price > 0)
                    {
                        _context.PresentationPriceHistories.Add(new PresentationPriceHistory
                        {
                            PresentationId = nueva.Id,
                            ProductId = productoExistente.Id,
                            OldPrice = 0,
                            NewPrice = nueva.Price,
                            UserId = userIdToken
                        });
                    }

                    _context.PresentationSupplierPriceHistories.Add(new PresentationSupplierPriceHistory
                    {
                        PresentationId = nueva.Id,
                        ProductId = productoExistente.Id,
                        OldSupplierPrice = 0,
                        NewSupplierPrice = nueva.SupplierPrice,
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

        //================================ Secciones de Ayuda al front =====================================

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

        // Obtener el nombre base de un producto para duplicarlo (sin la medida)
        public async Task<List<string>> GetNameSuggestionsAsync(string q)
        {
            q = (q ?? "").Trim();
            if (q.Length < 2)
                return new List<string>();

            // Nombres completos que empiezan con lo escrito (LIKE 'q%')
            var nombres = await _context.Products
                .AsNoTracking()
                .Where(p => p.IsActive && p.Name.StartsWith(q))
                .Select(p => p.Name)
                .Distinct()
                .OrderBy(n => n)
                .Take(300)
                .ToListAsync();

            var compare = CultureInfo.InvariantCulture.CompareInfo;

            return nombres
                .Select(ExtractBaseName)                                   // "abrazadera omega 3/4" -> "abrazadera omega"
                .Distinct(StringComparer.OrdinalIgnoreCase)                // una sola sugerencia por familia
                .Where(b => compare.IsPrefix(b, q,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace))
                .Take(10)
                .ToList();
        }

        // "abrazadera omega 1 1/4" -> "abrazadera omega"
        // Quita la medida del final: enteros, decimales, fracciones, mixtos y unidades comunes (", pulg, mm, cm)
        private static readonly Regex MedidaFinal = new(
            @"\s+\d+(?:[.,]\d+)?(?:\s+\d+/\d+|/\d+)?\s*(?:""|''|pulg\.?|mm|cm)?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static string ExtractBaseName(string nombre)
        {
            nombre = nombre.Trim();
            var baseName = MedidaFinal.Replace(nombre, "").Trim();

            // Si el nombre era solo una medida, no dejamos el campo vacío
            return baseName.Length > 0 ? baseName : nombre;
        }

        //================================ Fin de Secciones de Ayuda al front =====================================
    }
}