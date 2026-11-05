using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Enums;
using Api_Tlapaleria.Models;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Api_Tlapaleria.Services
{
    public class SaleService : ISaleService
    {
        private readonly TlapaleriaContext _context;
        private readonly IStockReservationService _stockReservationService;

        public SaleService(TlapaleriaContext context, IStockReservationService stockReservationService)
        {
            _context = context;
            _stockReservationService = stockReservationService;
        }

        //POST: Crear una nueva venta 
        public async Task<Sale> CreateSaleAsync(CreateSaleDto saleDto, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Creación de Folio único para cada transacción 
                var random = new Random();
                string folio = $"TKT-{DateTime.Now:yyMMddHHmmssfff}-{random.Next(1000, 10000)}";

                var venta = new Sale
                {
                    Folio = folio,
                    PaymentMethod = saleDto.PaymentMethod,
                    PaymentReference = saleDto.PaymentReference?.Trim(),
                    UserId = userId,
                    TotalAmount = 0,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                var itemsParaNotificar = new List<(int ProductId, int PresentationId, decimal QuantitySold, decimal NewCurrentStock)>();

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

                    // ====================================================================================
                    // ---> BLINDAJE: VALIDACIÓN DE FRACCIONES
                    // Si el producto NO permite fracciones (AllowFractions == false)
                    // Evaluamos si la cantidad trae decimales usando (item.Quantity % 1 != 0)
                    // ====================================================================================
                    if (!presentacion.Product.AllowFractions && (item.Quantity % 1 != 0))
                    {
                        throw new Exception($"El producto '{presentacion.Product.Name}' se vende por piezas enteras ({presentacion.Product.UnitOfMeasure}) y no permite vender cantidades con decimales (intentaste vender: {item.Quantity}).");
                    }
                    // 2. MATEMÁTICAS DE INVENTARIO
                    decimal cantidadBaseARestar = item.Quantity * presentacion.StockFactor;

                    // LÓGICA HÍBRIDA: Solo validamos si el producto lleva rastreo
                    if (presentacion.Product.IsInventoryTracked)
                    {
                        if (presentacion.Product.CurrentStock < cantidadBaseARestar)
                            throw new Exception($"Stock insuficiente. Quieres vender {item.Quantity} '{presentacion.Name}' (equivale a {cantidadBaseARestar} unidades base), pero solo hay {presentacion.Product.CurrentStock} en stock.");

                        decimal reservadoEnOtrasCajas = _stockReservationService.GetReservedStockExcluding(presentacion.Product.Id, userId, saleDto.ConnectionId);
                        decimal disponibleEnAnaquel = presentacion.Product.CurrentStock - reservadoEnOtrasCajas;

                        if (disponibleEnAnaquel < cantidadBaseARestar)
                            throw new Exception($"Stock apartado en otra caja. Quieres vender {item.Quantity} '{presentacion.Name}' ({cantidadBaseARestar} {presentacion.Product.UnitOfMeasure}), pero solo hay {Math.Max(0, disponibleEnAnaquel)} disponibles en anaquel ({reservadoEnOtrasCajas} apartados en otra caja).");
                    }

                    // 3. ARMAMOS LA LIBRETA (Ticket)

                    var detalle = new SaleDetail
                    {
                        ProductId = presentacion.Product.Id,
                        PresentationId = presentacion.Id,
                        SupplierId = presentacion.Product.SupplierId,
                        ProductName = $"{presentacion.Product.Name} - {presentacion.Name}",
                        Brand = presentacion.Product.Brand,
                        Quantity = item.Quantity,
                        StockFactorApplied = presentacion.StockFactor,
                        UnitPrice = presentacion.Price,
                        Subtotal = Math.Round(item.Quantity * presentacion.Price, 2, MidpointRounding.AwayFromZero),
                        SupplierPriceAtSale = presentacion.SupplierPrice, // <-- directo, sin fórmula
                        SupplierCostSubtotal = Math.Round(item.Quantity * presentacion.SupplierPrice, 2, MidpointRounding.AwayFromZero)
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
                            MovementType = MovementType.Venta,
                            Quantity = cantidadBaseARestar,
                            PreviousStock = stockAnterior,
                            NewStock = presentacion.Product.CurrentStock,
                            Notes = $"Ticket: {folio}. Se vendieron {item.Quantity} de la presentación '{presentacion.Name}'.",
                            CreatedAt = DateTime.Now
                        };

                        _context.InventoryMovements.Add(movimientoKardex);
                        itemsParaNotificar.Add((presentacion.Product.Id, presentacion.Id, item.Quantity, presentacion.Product.CurrentStock));
                    }
                }

                // ====================================================================================
                // DESCUENTO (clientes/familiares)
                // Se calcula sobre la suma de precios de lista y se PRORRATEA entre los renglones,
                // de modo que cada SaleDetail.Subtotal quede NETO. Así los reportes financieros y
                // las devoluciones (que reembolsan lo realmente pagado) siguen siendo consistentes.
                // ====================================================================================
                decimal subtotalBruto = venta.TotalAmount;
                decimal descuentoPorcentaje = saleDto.DiscountPercent ?? 0m;
                decimal descuentoMonto = saleDto.DiscountAmount ?? 0m;

                if (descuentoPorcentaje > 0 && descuentoMonto > 0)
                    throw new Exception("Aplica el descuento por porcentaje o por monto fijo, no ambos a la vez.");

                if (descuentoPorcentaje > 100m)
                    throw new Exception("El porcentaje de descuento no puede ser mayor al 100%.");

                if (descuentoPorcentaje > 0)
                    descuentoMonto = Math.Round(subtotalBruto * descuentoPorcentaje / 100m, 2, MidpointRounding.AwayFromZero);

                if (descuentoMonto > subtotalBruto)
                    throw new Exception($"El descuento (${descuentoMonto:N2}) no puede ser mayor al total de la venta (${subtotalBruto:N2}).");

                if (descuentoMonto > 0)
                {
                    decimal descuentoAsignado = 0m;
                    for (int i = 0; i < venta.Details.Count; i++)
                    {
                        var linea = venta.Details[i];
                        decimal descuentoLinea = (i == venta.Details.Count - 1)
                            ? descuentoMonto - descuentoAsignado // la última absorbe el redondeo
                            : Math.Round(descuentoMonto * linea.Subtotal / subtotalBruto, 2, MidpointRounding.AwayFromZero);

                        linea.DiscountAmount = descuentoLinea;
                        linea.Subtotal -= descuentoLinea;
                        descuentoAsignado += descuentoLinea;
                    }

                    venta.DiscountAmount = descuentoMonto;
                    venta.DiscountPercent = descuentoPorcentaje;
                    venta.TotalAmount = subtotalBruto - descuentoMonto;
                }

                if (venta.PaymentMethod == "Tarjeta" || venta.PaymentMethod == "Transferencia")
                {
                    if (string.IsNullOrWhiteSpace(venta.PaymentReference))
                    {
                        throw new Exception($"Para pagos con {venta.PaymentMethod}, es obligatorio ingresar el número de autorización o referencia.");
                    }
                }
                else if (venta.PaymentMethod == "Efectivo")
                {
                    if (!string.IsNullOrWhiteSpace(venta.PaymentReference))
                    {
                        // Validamos que sea un número válido y que alcance para pagar la cuenta
                        if (decimal.TryParse(venta.PaymentReference, out decimal montoRecibido))
                        {
                            if (montoRecibido < venta.TotalAmount)
                            {
                                throw new Exception($"El monto recibido en efectivo (${montoRecibido:N2}) es menor al total de la venta (${venta.TotalAmount:N2}).");
                            }
                        }
                        else
                        {
                            throw new Exception("La referencia para pago en efectivo debe ser un número válido (ej. 500 o 1000).");
                        }
                    }
                    else
                    {
                        // Opcional: Si quieres que para efectivo sea obligatorio poner con cuánto pagaron, descomenta esto:
                        throw new Exception("Debes indicar el monto en efectivo con el que pagó el cliente.");
                    }
                }

                // AQUÍ ESTABA EL ERROR: Estas líneas se habían perdido al pegar
                _context.Sales.Add(venta);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (itemsParaNotificar.Count > 0)
                {
                    await _stockReservationService.ConsumeReservationsOnSaleAsync(userId, saleDto.ConnectionId, itemsParaNotificar);
                }

                await _context.Entry(venta).Reference(v => v.User).LoadAsync();
                return venta; // <-- El return que el compilador no encontraba
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
                .Include(s => s.Details)
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
        //GET: Buscar una venta por ID 
        public async Task<Sale?> GetSaleByIdAsync(int saleId)
        {
            var ticket = await _context.Sales
                .Include(s => s.User) // Traemos los datos del cajero 
                .Include(s => s.Details) // Traemos todos los productos de esta venta
                .FirstOrDefaultAsync(s => s.Id == saleId);

            return ticket;
        }

        // Resumen operativo diario para el Dashboard / HomeScreen
        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(int userId, bool canViewAllSales)
        {
            var now = DateTime.Now;
            var todayStart = now.Date;
            var todayEnd = now.Date.AddDays(1).AddTicks(-1);

            // 1. Consulta base de ventas de hoy
            var salesQuery = _context.Sales
                .AsNoTracking()
                .Where(s => s.IsActive && s.CreatedAt >= todayStart && s.CreatedAt <= todayEnd);

            var returnsQuery = _context.Returns
                .AsNoTracking()
                .Where(r => r.CreatedAt >= todayStart && r.CreatedAt <= todayEnd);

            if (!canViewAllSales)
            {
                salesQuery = salesQuery.Where(s => s.UserId == userId);
                returnsQuery = returnsQuery.Where(r => r.UserId == userId);
            }

            // Traemos las ventas de hoy con solo las columnas necesarias para calcular montos y agrupar en memoria
            var salesList = await salesQuery
                .Select(s => new
                {
                    s.Id,
                    s.TotalAmount,
                    PaymentMethod = s.PaymentMethod ?? "Efectivo"
                })
                .ToListAsync();

            var grossSales = salesList.Sum(s => s.TotalAmount);
            var totalTicketsCount = salesList.Count;

            // Reembolsos de hoy
            var totalRefunded = await returnsQuery
                .Select(r => (decimal?)r.TotalRefunded)
                .SumAsync() ?? 0m;

            var netSales = Math.Max(0m, grossSales - totalRefunded);

            // Desglose por método de pago agrupado en memoria
            var paymentMethods = salesList
                .GroupBy(s => s.PaymentMethod)
                .Select(g => new PaymentMethodSummaryDto
                {
                    Method = g.Key,
                    TotalAmount = g.Sum(s => s.TotalAmount),
                    Count = g.Count()
                })
                .OrderByDescending(pm => pm.TotalAmount)
                .ToList();

            // 2. Top productos más vendidos hoy
            var detailsQuery = _context.SaleDetails
                .AsNoTracking()
                .Where(d => d.Sale!.IsActive && d.Sale.CreatedAt >= todayStart && d.Sale.CreatedAt <= todayEnd);

            if (!canViewAllSales)
            {
                detailsQuery = detailsQuery.Where(d => d.Sale!.UserId == userId);
            }

            var rawDetails = await detailsQuery
                .Select(d => new
                {
                    d.ProductName,
                    d.Brand,
                    d.Quantity,
                    d.Subtotal
                })
                .ToListAsync();

            var topProducts = rawDetails
                .GroupBy(d => new { d.ProductName, d.Brand })
                .Select(g => new DashboardTopProductDto
                {
                    ProductName = g.Key.ProductName,
                    Brand = g.Key.Brand,
                    TotalQuantity = g.Sum(d => d.Quantity),
                    TotalAmount = g.Sum(d => d.Subtotal)
                })
                .OrderByDescending(tp => tp.TotalQuantity)
                .Take(5)
                .ToList();

            // 3. Alertas de Stock
            var stockAlerts = new List<StockAlertDto>();

            // A) Productos con stock bajo (<= 5)
            var lowStockList = await _context.Products
                .AsNoTracking()
                .Where(p => p.IsActive && p.IsInventoryTracked && p.CurrentStock <= 5)
                .OrderBy(p => p.CurrentStock)
                .Take(5)
                .Select(p => new
                {
                    p.Id,
                    p.InternalCode,
                    p.Name,
                    p.CurrentStock,
                    p.UnitOfMeasure
                })
                .ToListAsync();

            stockAlerts.AddRange(lowStockList.Select(p => new StockAlertDto
            {
                ProductId = p.Id,
                InternalCode = p.InternalCode,
                Name = p.Name,
                CurrentStock = p.CurrentStock,
                UnitOfMeasure = p.UnitOfMeasure,
                AlertType = p.CurrentStock <= 0 ? "OutOfStock" : "LowStock",
                Message = p.CurrentStock <= 0
                    ? $"Agotado (0 {p.UnitOfMeasure})"
                    : $"Inventario bajo: {p.CurrentStock} {p.UnitOfMeasure} restantes"
            }));

            // B) Próximos a caducar en los próximos 30 días
            var expireLimit = now.Date.AddDays(30);
            var expiringRaw = await _context.Products
                .AsNoTracking()
                .Where(p => p.IsActive && p.IsInventoryTracked && p.HasExpiration && p.NextExpirationDate != null && p.NextExpirationDate <= expireLimit && p.CurrentStock > 0)
                .OrderBy(p => p.NextExpirationDate)
                .Take(5)
                .Select(p => new
                {
                    p.Id,
                    p.InternalCode,
                    p.Name,
                    p.CurrentStock,
                    p.UnitOfMeasure,
                    p.NextExpirationDate
                })
                .ToListAsync();

            stockAlerts.AddRange(expiringRaw.Select(p =>
            {
                int days = (p.NextExpirationDate!.Value.Date - now.Date).Days;
                return new StockAlertDto
                {
                    ProductId = p.Id,
                    InternalCode = p.InternalCode,
                    Name = p.Name,
                    CurrentStock = p.CurrentStock,
                    UnitOfMeasure = p.UnitOfMeasure,
                    AlertType = "ExpiringSoon",
                    DaysRemaining = days,
                    Message = days <= 0
                        ? "Vencido hoy o en fecha anterior"
                        : $"Próximo a caducar en {days} días"
                };
            }));

            return new DashboardSummaryDto
            {
                TotalSalesAmount = netSales,
                GrossSalesAmount = grossSales,
                TotalRefundedAmount = totalRefunded,
                TotalTicketsCount = totalTicketsCount,
                IsGlobal = canViewAllSales,
                ScopeLabel = canViewAllSales ? "Ventas Globales (Toda la tienda)" : "Mis Ventas (Tu turno)",
                PaymentMethods = paymentMethods,
                TopSellingProducts = topProducts,
                StockAlerts = stockAlerts
            };
        }
    }
}
