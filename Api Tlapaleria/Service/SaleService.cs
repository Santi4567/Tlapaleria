using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class SaleService : ISaleService
    {
        private readonly TlapaleriaContext _context;

        public SaleService(TlapaleriaContext context)
        {
            _context = context;
        }

        public async Task<Sale> CreateSaleAsync(CreateSaleDto saleDto, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                //Creacion de Folio unico para cada transaccion 
                var random = new Random();
                string folio = $"TKT-{DateTime.Now:yyMMddHHmmssfff}-{random.Next(1000, 10000)}";

                var venta = new Sale
                {
                    Folio = folio,
                    PaymentMethod = saleDto.PaymentMethod,
                    UserId = userId,
                    TotalAmount = 0,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                foreach (var item in saleDto.Details)
                {
                    // 1. Buscamos la Presentación Y TAMBIÉN su Producto Base
                    var presentacion = await _context.ProductPresentations
                        .Include(p => p.Product)
                        .FirstOrDefaultAsync(p => p.Id == item.PresentationId);

                    // Validamos que exista la presentación y el producto base
                    if (presentacion == null || !presentacion.IsActive)
                        throw new Exception($"La presentación con ID {item.PresentationId} no existe o está inactiva.");

                    if (presentacion.Product == null || !presentacion.Product.IsActive)
                        throw new Exception($"El producto base para la presentación '{presentacion.Name}' no está disponible.");

                    // 2. MATEMÁTICAS DE INVENTARIO
                    decimal cantidadBaseARestar = item.Quantity * presentacion.StockFactor;

                    // LÓGICA HÍBRIDA: Solo validamos y restamos si el producto lleva rastreo
                    if (presentacion.Product.IsInventoryTracked)
                    {
                        if (presentacion.Product.CurrentStock < cantidadBaseARestar)
                            throw new Exception($"Stock insuficiente. Quieres vender {item.Quantity} '{presentacion.Name}' (equivale a {cantidadBaseARestar} unidades base), pero solo hay {presentacion.Product.CurrentStock} en stock.");
                    }

                    // 3. ARMAMOS LA LIBRETA (Ticket)
                    var detalle = new SaleDetail
                    {
                        ProductId = presentacion.Product.Id,
                        PresentationId = presentacion.Id,
                        ProductName = $"{presentacion.Product.Name} - {presentacion.Name}",
                        Brand = presentacion.Product.Brand,
                        Quantity = item.Quantity,
                        StockFactorApplied = presentacion.StockFactor,
                        UnitPrice = presentacion.Price,
                        Subtotal = item.Quantity * presentacion.Price
                    };

                    venta.TotalAmount += detalle.Subtotal;
                    venta.Details.Add(detalle);

                    // 4. ACTUALIZAMOS EL INVENTARIO BASE Y EL KARDEX
                    if (presentacion.Product.IsInventoryTracked)
                    {
                        decimal stockAnterior = presentacion.Product.CurrentStock;
                        presentacion.Product.CurrentStock -= cantidadBaseARestar;

                        var movimientoKardex = new InventoryMovement
                        {
                            ProductId = presentacion.Product.Id,
                            UserId = userId,
                            MovementType = "Venta",
                            Quantity = cantidadBaseARestar,
                            PreviousStock = stockAnterior,
                            NewStock = presentacion.Product.CurrentStock,
                            Notes = $"Ticket: {folio}. Se vendieron {item.Quantity} de la presentación '{presentacion.Name}'.",
                            CreatedAt = DateTime.Now
                        };

                        _context.InventoryMovements.Add(movimientoKardex);
                    }

                    _context.Sales.Add(venta);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _context.Entry(venta).Reference(v => v.User).LoadAsync();
                return venta;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        // Busqueda de Venta en tabla Sale
        // consulta flexible.
        // usamos .Contains() para que si el folio es TKT-2604111609-8492,
        // el cajero pueda encontrarlo con solo teclear 8492 o 260411.
        public async Task<PagedResponse<Sale>> GetSalesAsync(string? searchFolio = null, int pageNumber = 1, int pageSize = 50)
        {
            // Empezamos armando la consulta, incluyendo al usuario para saber quién cobró
            var query = _context.Sales
                .Include(s => s.User)
                .AsQueryable();

            // Si el frontend nos mandó algo en la caja de búsqueda, filtramos
            if (!string.IsNullOrWhiteSpace(searchFolio))
            {
                query = query.Where(s => s.Folio.Contains(searchFolio));
            }

            // Contamos el total de tickets para las matemáticas de la paginación
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Traemos la página solicitada, ordenando siempre del más nuevo al más viejo
            var sales = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResponse<Sale>
            {
                Data = sales,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };
        }

        public async Task<Sale?> GetSaleByIdAsync(int saleId)
        {
            var ticket = await _context.Sales
                .Include(s => s.User) // Traemos los datos del cajero 
                .Include(s => s.Details) // ¡MAGIA! Traemos todos los productos de esta venta
                .FirstOrDefaultAsync(s => s.Id == saleId);

            return ticket;
        }
    }
}