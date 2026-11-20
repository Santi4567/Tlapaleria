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

        // POST: Crear una nueva venta (Contado o Crédito)
        public async Task<Sale> CreateSaleAsync(CreateSaleDto saleDto, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                Customer? cliente = null;
                if (saleDto.CustomerId.HasValue && saleDto.CustomerId.Value > 0)
                {
                    cliente = await _context.Customers
                        .FirstOrDefaultAsync(c => c.Id == saleDto.CustomerId.Value);

                    if (cliente == null || !cliente.IsActive)
                        throw new Exception("El cliente seleccionado no existe o se encuentra inactivo.");
                }

                if (saleDto.PaymentMethod == "Credito" && cliente == null)
                {
                    throw new Exception("Para realizar una venta a crédito (fiado), es obligatorio seleccionar un cliente de confianza.");
                }

                // Creación de Folio único para cada transacción 
                var random = new Random();
                string folio = $"TKT-{DateTime.Now:yyMMddHHmmssfff}-{random.Next(1000, 10000)}";

                var venta = new Sale
                {
                    Folio = folio,
                    PaymentMethod = saleDto.PaymentMethod,
                    PaymentReference = saleDto.PaymentReference?.Trim(),
                    UserId = userId,
                    CustomerId = cliente?.Id,
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
                            Notes = saleDto.PaymentMethod == "Credito"
                                ? $"Ticket a Crédito: {folio} (Cliente: {cliente!.Name}). Se entregaron {item.Quantity} de '{presentacion.Name}'."
                                : $"Ticket: {folio}. Se vendieron {item.Quantity} de la presentación '{presentacion.Name}'.",
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

                // ====================================================================================
                // VALIDACIÓN DE MÉTODO DE PAGO (CONTADO VS CRÉDITO)
                // ====================================================================================
                decimal anticipoCredito = 0m;
                string metodoAnticipo = "Efectivo";

                if (venta.PaymentMethod == "Credito")
                {
                    if (venta.TotalAmount <= 0)
                        throw new Exception("No se puede abrir una cuenta de crédito por un monto de $0.00.");

                    anticipoCredito = Math.Round(saleDto.InitialPayment ?? 0m, 2, MidpointRounding.AwayFromZero);

                    if (anticipoCredito < 0)
                        throw new Exception("El anticipo inicial no puede ser negativo.");

                    if (anticipoCredito >= venta.TotalAmount)
                        throw new Exception($"El anticipo (${anticipoCredito:N2}) cubre el total de la venta (${venta.TotalAmount:N2}). Regístrala como una venta de contado.");

                    decimal deudaNetaGenerada = venta.TotalAmount - anticipoCredito;

                    // Validación opcional de límite de crédito del cliente
                    if (cliente!.CreditLimit.HasValue && cliente.CreditLimit.Value > 0)
                    {
                        decimal deudaActualCliente = await _context.CreditAccounts
                            .Where(ca => ca.CustomerId == cliente.Id && ca.Balance > 0 && ca.Status != CreditAccountStatus.Cancelado.ToString())
                            .SumAsync(ca => (decimal?)ca.Balance) ?? 0m;

                        if (deudaActualCliente + deudaNetaGenerada > cliente.CreditLimit.Value)
                        {
                            decimal disponible = Math.Max(0m, cliente.CreditLimit.Value - deudaActualCliente);
                            throw new Exception(
                                $"El cliente '{cliente.Name}' supera su límite de crédito autorizado (${cliente.CreditLimit.Value:N2}). " +
                                $"Deuda actual: ${deudaActualCliente:N2} | Disponible: ${disponible:N2} | Nuevo saldo a financiar: ${deudaNetaGenerada:N2}.");
                        }
                    }

                    if (anticipoCredito > 0)
                    {
                        metodoAnticipo = string.IsNullOrWhiteSpace(saleDto.InitialPaymentMethod) ? "Efectivo" : saleDto.InitialPaymentMethod;
                        if ((metodoAnticipo == "Tarjeta" || metodoAnticipo == "Transferencia") && string.IsNullOrWhiteSpace(venta.PaymentReference))
                        {
                            throw new Exception($"Para el anticipo con {metodoAnticipo}, es obligatorio ingresar el número de autorización o referencia.");
                        }
                    }
                }
                else if (venta.PaymentMethod == "Tarjeta" || venta.PaymentMethod == "Transferencia")
                {
                    if (venta.TotalAmount == 0 && string.IsNullOrWhiteSpace(venta.PaymentReference))
                    {
                        venta.PaymentReference = "DESCUENTO 100%";
                    }
                    else if (string.IsNullOrWhiteSpace(venta.PaymentReference))
                    {
                        throw new Exception($"Para pagos con {venta.PaymentMethod}, es obligatorio ingresar el número de autorización o referencia.");
                    }
                }
                else if (venta.PaymentMethod == "Efectivo")
                {
                    if (venta.TotalAmount == 0 && string.IsNullOrWhiteSpace(venta.PaymentReference))
                    {
                        venta.PaymentReference = "0";
                    }

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
                        throw new Exception("Debes indicar el monto en efectivo con el que pagó el cliente.");
                    }
                }

                _context.Sales.Add(venta);
                await _context.SaveChangesAsync();

                // Si es una venta a crédito, creamos su Cuenta por Cobrar (CreditAccount) y su anticipo si dejó
                if (venta.PaymentMethod == "Credito" && cliente != null)
                {
                    string folioCuenta = $"CRD-{DateTime.Now:yyMMddHHmmssfff}-{random.Next(1000, 10000)}";
                    decimal saldoPendiente = venta.TotalAmount - anticipoCredito;

                    var cuentaCredito = new CreditAccount
                    {
                        Folio = folioCuenta,
                        CustomerId = cliente.Id,
                        SaleId = venta.Id,
                        UserId = userId,
                        OriginalAmount = venta.TotalAmount,
                        ReturnedAmount = 0m,
                        PaidAmount = anticipoCredito,
                        Balance = saldoPendiente,
                        Status = anticipoCredito > 0
                            ? CreditAccountStatus.Abonado.ToString()
                            : CreditAccountStatus.Pendiente.ToString(),
                        DueDate = saleDto.DueDate,
                        Notes = saleDto.CreditNotes?.Trim(),
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    if (anticipoCredito > 0)
                    {
                        string folioAbono = $"ABN-{DateTime.Now:yyMMddHHmmssfff}-{random.Next(1000, 10000)}";
                        var pagoInicial = new CreditPayment
                        {
                            PaymentFolio = folioAbono,
                            CustomerId = cliente.Id,
                            UserId = userId,
                            Amount = anticipoCredito,
                            PaymentMethod = metodoAnticipo,
                            PaymentReference = venta.PaymentReference,
                            Notes = $"Anticipo inicial al adquirir la venta {venta.Folio}",
                            CreatedAt = DateTime.Now
                        };
                        cuentaCredito.Payments.Add(pagoInicial);
                    }

                    _context.CreditAccounts.Add(cuentaCredito);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                if (itemsParaNotificar.Count > 0)
                {
                    await _stockReservationService.ConsumeReservationsOnSaleAsync(userId, saleDto.ConnectionId, itemsParaNotificar);
                }

                await _context.Entry(venta).Reference(v => v.User).LoadAsync();
                if (venta.CustomerId.HasValue)
                {
                    await _context.Entry(venta).Reference(v => v.Customer).LoadAsync();
                }

                return venta;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Búsqueda de Venta en tabla Sale
        public async Task<PagedResponse<Sale>> GetSalesAsync(string? searchFolio = null, int pageNumber = 1, int pageSize = 50)
        {
            var query = _context.Sales
                .Include(s => s.User)
                .Include(s => s.Customer)
                .Include(s => s.Details)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchFolio))
            {
                string term = searchFolio.Trim();
                query = query.Where(s =>
                    s.Folio.Contains(term) ||
                    (s.Customer != null && s.Customer.Name.Contains(term)));
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

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

        // GET: Buscar una venta por ID 
        public async Task<Sale?> GetSaleByIdAsync(int saleId)
        {
            var ticket = await _context.Sales
                .Include(s => s.User)
                .Include(s => s.Customer)
                .Include(s => s.Details)
                .FirstOrDefaultAsync(s => s.Id == saleId);

            return ticket;
        }

        // Resumen operativo diario para el Dashboard / HomeScreen
        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(int userId, bool canViewAllSales)
        {
            var now = DateTime.Now;
            var todayStart = now.Date;
            var todayEnd = now.Date.AddDays(1).AddTicks(-1);

            // 1. Consulta base de ventas y devoluciones de hoy
            var salesQuery = _context.Sales
                .AsNoTracking()
                .Where(s => s.IsActive && s.CreatedAt >= todayStart && s.CreatedAt <= todayEnd);

            var returnsQuery = _context.Returns
                .AsNoTracking()
                .Where(r => r.CreatedAt >= todayStart && r.CreatedAt <= todayEnd);

            var creditPaymentsTodayQuery = _context.CreditPayments
                .AsNoTracking()
                .Where(cp => cp.CreatedAt >= todayStart && cp.CreatedAt <= todayEnd);

            if (!canViewAllSales)
            {
                salesQuery = salesQuery.Where(s => s.UserId == userId);
                returnsQuery = returnsQuery.Where(r => r.UserId == userId);
                creditPaymentsTodayQuery = creditPaymentsTodayQuery.Where(cp => cp.UserId == userId);
            }

            var salesList = await salesQuery
                .Select(s => new
                {
                    s.Id,
                    s.TotalAmount,
                    PaymentMethod = s.PaymentMethod ?? "Efectivo"
                })
                .ToListAsync();

            var creditPaymentsToday = await creditPaymentsTodayQuery
                .Select(cp => new
                {
                    cp.Amount,
                    PaymentMethod = cp.PaymentMethod ?? "Efectivo"
                })
                .ToListAsync();

            // Separamos ventas de contado vs ventas a crédito
            // Regla de negocio: En Caja/Ingresos entra el dinero de ventas de contado + los anticipos/abonos cobrados hoy
            var cashSalesList = salesList.Where(s => s.PaymentMethod != "Credito").ToList();
            var creditSalesList = salesList.Where(s => s.PaymentMethod == "Credito").ToList();

            var creditSalesTodayAmount = creditSalesList.Sum(s => s.TotalAmount);
            var creditPaymentsCollectedToday = creditPaymentsToday.Sum(cp => cp.Amount);

            var grossSales = cashSalesList.Sum(s => s.TotalAmount) + creditPaymentsCollectedToday;
            var totalTicketsCount = salesList.Count;

            // Reembolsos en efectivo de hoy (para ventas a crédito solo resta si se devolvió efectivo excedente, no lo que solo descontó deuda)
            var returnsTodayList = await returnsQuery
                .Select(r => new
                {
                    r.TotalRefunded,
                    r.AppliedToDebtAmount,
                    r.CashRefundedAmount,
                    SalePaymentMethod = r.Sale != null ? r.Sale.PaymentMethod : "Efectivo"
                })
                .ToListAsync();

            decimal totalRefunded = returnsTodayList.Sum(r =>
                r.SalePaymentMethod == "Credito" ? r.CashRefundedAmount : r.TotalRefunded);

            var netSales = Math.Max(0m, grossSales - totalRefunded);

            // Desglose por método de pago en caja (ventas de contado + abonos recibidos por su método de pago)
            var cashAndPaymentsCombined = cashSalesList
                .Select(s => new { Method = s.PaymentMethod, Amount = s.TotalAmount })
                .Concat(creditPaymentsToday.Select(cp => new { Method = cp.PaymentMethod, Amount = cp.Amount }));

            var paymentMethods = cashAndPaymentsCombined
                .GroupBy(x => x.Method)
                .Select(g => new PaymentMethodSummaryDto
                {
                    Method = g.Key,
                    TotalAmount = g.Sum(x => x.Amount),
                    Count = g.Count()
                })
                .OrderByDescending(pm => pm.TotalAmount)
                .ToList();

            // Si hubo ventas a crédito hoy, también agregamos el indicador informativo de cuántos tickets a crédito salieron
            if (creditSalesList.Any())
            {
                paymentMethods.Add(new PaymentMethodSummaryDto
                {
                    Method = "Credito (Por Cobrar)",
                    TotalAmount = creditSalesTodayAmount,
                    Count = creditSalesList.Count
                });
            }

            decimal totalPendingReceivables = await _context.CreditAccounts
                .AsNoTracking()
                .Where(ca => ca.Balance > 0 && ca.Status != CreditAccountStatus.Cancelado.ToString())
                .SumAsync(ca => (decimal?)ca.Balance) ?? 0m;

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
                CreditSalesAmountToday = creditSalesTodayAmount,
                CreditPaymentsCollectedToday = creditPaymentsCollectedToday,
                TotalPendingReceivables = totalPendingReceivables,
                IsGlobal = canViewAllSales,
                ScopeLabel = canViewAllSales ? "Ventas Globales (Toda la tienda)" : "Mis Ventas (Tu turno)",
                PaymentMethods = paymentMethods,
                TopSellingProducts = topProducts,
                StockAlerts = stockAlerts
            };
        }
    }
}
