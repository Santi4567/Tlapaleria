using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Enums;
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class CreditAccountService : ICreditAccountService
    {
        private readonly TlapaleriaContext _context;

        public CreditAccountService(TlapaleriaContext context)
        {
            _context = context;
        }

        // Resumen global de la cartera de crédito (para las tarjetas superiores de la sección de Cuentas)
        public async Task<CreditPortfolioSummaryDto> GetPortfolioSummaryAsync()
        {
            var now = DateTime.Now;
            var todayStart = now.Date;
            var todayEnd = now.Date.AddDays(1).AddTicks(-1);

            var activeAccounts = await _context.CreditAccounts
                .AsNoTracking()
                .Where(ca => ca.Balance > 0 && ca.Status != CreditAccountStatus.Cancelado.ToString())
                .Select(ca => new
                {
                    ca.CustomerId,
                    ca.Balance,
                    ca.DueDate
                })
                .ToListAsync();

            var totalReceivable = activeAccounts.Sum(a => a.Balance);
            var customersWithDebt = activeAccounts.Select(a => a.CustomerId).Distinct().Count();
            var activeCount = activeAccounts.Count;

            var overdueList = activeAccounts
                .Where(a => a.DueDate.HasValue && a.DueDate.Value.Date < todayStart)
                .ToList();

            var creditGrantedToday = await _context.CreditAccounts
                .AsNoTracking()
                .Where(ca => ca.CreatedAt >= todayStart && ca.CreatedAt <= todayEnd && ca.Status != CreditAccountStatus.Cancelado.ToString())
                .SumAsync(ca => (decimal?)ca.OriginalAmount) ?? 0m;

            var collectedToday = await _context.CreditPayments
                .AsNoTracking()
                .Where(cp => cp.CreatedAt >= todayStart && cp.CreatedAt <= todayEnd)
                .SumAsync(cp => (decimal?)cp.Amount) ?? 0m;

            return new CreditPortfolioSummaryDto
            {
                TotalReceivableBalance = totalReceivable,
                CustomersWithDebtCount = customersWithDebt,
                ActiveAccountsCount = activeCount,
                OverdueAccountsCount = overdueList.Count,
                OverdueBalance = overdueList.Sum(a => a.Balance),
                CreditGrantedTodayAmount = creditGrantedToday,
                CollectedTodayAmount = collectedToday
            };
        }

        // Listado paginado de cuentas con filtros (por cliente, por estado, vencidas, búsqueda por folio/nombre)
        public async Task<PagedResponse<CreditAccountDetailDto>> GetAccountsAsync(
            int? customerId = null,
            string? status = null,
            bool onlyOverdue = false,
            string? search = null,
            int pageNumber = 1,
            int pageSize = 20)
        {
            var todayStart = DateTime.Now.Date;

            var query = _context.CreditAccounts
                .AsNoTracking()
                .Include(ca => ca.Customer)
                .Include(ca => ca.User)
                .Include(ca => ca.Sale)
                    .ThenInclude(s => s!.Details)
                .Include(ca => ca.Payments)
                    .ThenInclude(p => p.User)
                .AsQueryable();

            if (customerId.HasValue && customerId.Value > 0)
            {
                query = query.Where(ca => ca.CustomerId == customerId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "Todos", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(status, "Activas", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "ConDeuda", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(ca => ca.Balance > 0 && ca.Status != CreditAccountStatus.Cancelado.ToString());
                }
                else
                {
                    query = query.Where(ca => ca.Status == status);
                }
            }

            if (onlyOverdue)
            {
                query = query.Where(ca => ca.Balance > 0 && ca.DueDate != null && ca.DueDate < todayStart);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                query = query.Where(ca =>
                    ca.Folio.Contains(term) ||
                    (ca.Sale != null && ca.Sale.Folio.Contains(term)) ||
                    (ca.Customer != null && ca.Customer.Name.Contains(term)) ||
                    (ca.Customer != null && ca.Customer.Alias != null && ca.Customer.Alias.Contains(term)) ||
                    (ca.Customer != null && ca.Customer.Phone != null && ca.Customer.Phone.Contains(term)) ||
                    (ca.Notes != null && ca.Notes.Contains(term))
                );
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Ordenamos: primero las cuentas con saldo pendiente (más recientes o por vencer), luego las liquidadas
            var accounts = await query
                .OrderByDescending(ca => ca.Balance > 0)
                .ThenByDescending(ca => ca.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtoList = await MapAccountsToDetailDtosAsync(accounts);

            return new PagedResponse<CreditAccountDetailDto>
            {
                Data = dtoList,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };
        }

        // Obtener el detalle completo de una cuenta por su ID
        public async Task<CreditAccountDetailDto> GetAccountByIdAsync(int accountId)
        {
            var account = await _context.CreditAccounts
                .AsNoTracking()
                .Include(ca => ca.Customer)
                .Include(ca => ca.User)
                .Include(ca => ca.Sale)
                    .ThenInclude(s => s!.Details)
                .Include(ca => ca.Payments)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(ca => ca.Id == accountId);

            if (account == null)
                throw new KeyNotFoundException($"La cuenta de crédito con ID {accountId} no existe.");

            var mapped = await MapAccountsToDetailDtosAsync(new List<CreditAccount> { account });
            return mapped.First();
        }

        // Obtener la cuenta de crédito asociada a un ticket de venta específico
        public async Task<CreditAccountDetailDto?> GetAccountBySaleIdAsync(int saleId)
        {
            var account = await _context.CreditAccounts
                .AsNoTracking()
                .Include(ca => ca.Customer)
                .Include(ca => ca.User)
                .Include(ca => ca.Sale)
                    .ThenInclude(s => s!.Details)
                .Include(ca => ca.Payments)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(ca => ca.SaleId == saleId);

            if (account == null) return null;

            var mapped = await MapAccountsToDetailDtosAsync(new List<CreditAccount> { account });
            return mapped.First();
        }

        // Registrar un abono a una cuenta específica (por ticket)
        public async Task<CreditAccountDetailDto> RegisterAccountPaymentAsync(int accountId, RegisterCreditPaymentDto dto, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var account = await _context.CreditAccounts
                    .Include(ca => ca.Customer)
                    .Include(ca => ca.Sale)
                    .FirstOrDefaultAsync(ca => ca.Id == accountId);

                if (account == null)
                    throw new KeyNotFoundException($"La cuenta de crédito con ID {accountId} no existe.");

                if (account.Balance <= 0 || account.Status == CreditAccountStatus.Liquidado.ToString() || account.Status == CreditAccountStatus.Cancelado.ToString())
                    throw new InvalidOperationException($"Esta cuenta ({account.Folio}) ya se encuentra liquidada o sin saldo pendiente ($0.00).");

                decimal montoAbono = Math.Round(dto.Amount, 2, MidpointRounding.AwayFromZero);

                if (montoAbono <= 0)
                    throw new InvalidOperationException("El monto a abonar debe ser mayor a $0.00.");

                if (montoAbono > account.Balance)
                {
                    throw new InvalidOperationException(
                        $"El monto del abono (${montoAbono:N2}) supera el saldo pendiente de esta cuenta (${account.Balance:N2}). " +
                        $"Si el cliente desea abonar a varias cuentas a la vez, utiliza el abono al saldo general del cliente.");
                }

                ValidarMetodoYReferenciaAbono(dto.PaymentMethod, dto.PaymentReference, montoAbono);

                var random = new Random();
                string paymentFolio = $"ABN-{DateTime.Now:yyMMddHHmmssfff}-{random.Next(1000, 10000)}";

                var pago = new CreditPayment
                {
                    PaymentFolio = paymentFolio,
                    CreditAccountId = account.Id,
                    CustomerId = account.CustomerId,
                    UserId = userId,
                    Amount = montoAbono,
                    PaymentMethod = dto.PaymentMethod,
                    PaymentReference = dto.PaymentReference?.Trim(),
                    Notes = dto.Notes?.Trim(),
                    CreatedAt = DateTime.Now
                };

                _context.CreditPayments.Add(pago);

                account.PaidAmount += montoAbono;
                account.Balance = Math.Max(0m, account.OriginalAmount - account.ReturnedAmount - account.PaidAmount);
                account.UpdatedAt = DateTime.Now;

                if (account.Balance == 0m)
                {
                    account.Status = CreditAccountStatus.Liquidado.ToString();
                    account.PaidAt = DateTime.Now;
                }
                else
                {
                    account.Status = CreditAccountStatus.Abonado.ToString();
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await GetAccountByIdAsync(account.Id);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Registrar un abono al saldo general del cliente (reparte automáticamente en orden FIFO: de la cuenta más antigua a la más nueva)
        public async Task<GeneralCustomerPaymentResultDto> RegisterGeneralCustomerPaymentAsync(int customerId, RegisterCreditPaymentDto dto, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Id == customerId);

                if (customer == null)
                    throw new KeyNotFoundException($"El cliente con ID {customerId} no existe.");

                var cuentasPendientes = await _context.CreditAccounts
                    .Include(ca => ca.Sale)
                    .Where(ca => ca.CustomerId == customerId && ca.Balance > 0 && ca.Status != CreditAccountStatus.Cancelado.ToString())
                    .OrderBy(ca => ca.CreatedAt) // FIFO: primero se paga la deuda más antigua
                    .ThenBy(ca => ca.Id)
                    .ToListAsync();

                if (!cuentasPendientes.Any())
                    throw new InvalidOperationException($"El cliente '{customer.Name}' no tiene ninguna cuenta con deuda pendiente actualmente.");

                decimal deudaTotalActual = cuentasPendientes.Sum(ca => ca.Balance);
                decimal montoTotalAAbonar = Math.Round(dto.Amount, 2, MidpointRounding.AwayFromZero);

                if (montoTotalAAbonar <= 0)
                    throw new InvalidOperationException("El monto a abonar debe ser mayor a $0.00.");

                if (montoTotalAAbonar > deudaTotalActual)
                {
                    throw new InvalidOperationException(
                        $"El monto del abono (${montoTotalAAbonar:N2}) supera la deuda total acumulada del cliente '{customer.Name}' (${deudaTotalActual:N2}).");
                }

                ValidarMetodoYReferenciaAbono(dto.PaymentMethod, dto.PaymentReference, montoTotalAAbonar);

                var cajero = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
                string nombreCajero = cajero?.Name ?? "Cajero";

                decimal remanentePorAplicar = montoTotalAAbonar;
                int cuentasAfectadas = 0;
                int cuentasLiquidadas = 0;
                var pagosGenerados = new List<(CreditPayment Payment, CreditAccount Account)>();
                var random = new Random();

                foreach (var cuenta in cuentasPendientes)
                {
                    if (remanentePorAplicar <= 0) break;

                    decimal abonoACuenta = Math.Min(remanentePorAplicar, cuenta.Balance);
                    remanentePorAplicar -= abonoACuenta;
                    cuentasAfectadas++;

                    string paymentFolio = $"ABN-{DateTime.Now:yyMMddHHmmssfff}-{random.Next(1000, 10000)}";
                    string notaAutomatica = string.IsNullOrWhiteSpace(dto.Notes)
                        ? $"Abono general (${montoTotalAAbonar:N2}) aplicado a cuenta {cuenta.Folio}"
                        : $"{dto.Notes.Trim()} (Abono general ${montoTotalAAbonar:N2})";

                    var pago = new CreditPayment
                    {
                        PaymentFolio = paymentFolio,
                        CreditAccountId = cuenta.Id,
                        CustomerId = customer.Id,
                        UserId = userId,
                        Amount = abonoACuenta,
                        PaymentMethod = dto.PaymentMethod,
                        PaymentReference = dto.PaymentReference?.Trim(),
                        Notes = notaAutomatica,
                        CreatedAt = DateTime.Now
                    };

                    _context.CreditPayments.Add(pago);

                    cuenta.PaidAmount += abonoACuenta;
                    cuenta.Balance = Math.Max(0m, cuenta.OriginalAmount - cuenta.ReturnedAmount - cuenta.PaidAmount);
                    cuenta.UpdatedAt = DateTime.Now;

                    if (cuenta.Balance == 0m)
                    {
                        cuenta.Status = CreditAccountStatus.Liquidado.ToString();
                        cuenta.PaidAt = DateTime.Now;
                        cuentasLiquidadas++;
                    }
                    else
                    {
                        cuenta.Status = CreditAccountStatus.Abonado.ToString();
                    }

                    pagosGenerados.Add((pago, cuenta));
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var appliedDtos = pagosGenerados.Select(x => new CreditPaymentDto
                {
                    Id = x.Payment.Id,
                    PaymentFolio = x.Payment.PaymentFolio,
                    CreditAccountId = x.Account.Id,
                    CreditAccountFolio = x.Account.Folio,
                    SaleId = x.Account.SaleId,
                    SaleFolio = x.Account.Sale?.Folio ?? string.Empty,
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    Amount = x.Payment.Amount,
                    PaymentMethod = x.Payment.PaymentMethod,
                    PaymentReference = x.Payment.PaymentReference,
                    Notes = x.Payment.Notes,
                    ReceivedByUserName = nombreCajero,
                    CreatedAt = x.Payment.CreatedAt
                }).ToList();

                return new GeneralCustomerPaymentResultDto
                {
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    TotalAmountApplied = montoTotalAAbonar,
                    PreviousTotalDebt = deudaTotalActual,
                    RemainingTotalDebt = deudaTotalActual - montoTotalAAbonar,
                    AccountsAffectedCount = cuentasAfectadas,
                    AccountsSettledCount = cuentasLiquidadas,
                    AppliedPayments = appliedDtos
                };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Actualizar fecha compromiso de pago o notas de una cuenta
        public async Task<CreditAccountDetailDto> UpdateAccountMetadataAsync(int accountId, UpdateCreditAccountDto dto)
        {
            var account = await _context.CreditAccounts.FirstOrDefaultAsync(ca => ca.Id == accountId);
            if (account == null)
                throw new KeyNotFoundException($"La cuenta de crédito con ID {accountId} no existe.");

            account.DueDate = dto.DueDate;
            account.Notes = dto.Notes?.Trim();
            account.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return await GetAccountByIdAsync(accountId);
        }

        // Historial paginado de todos los abonos recibidos
        public async Task<PagedResponse<CreditPaymentDto>> GetPaymentsHistoryAsync(
            int? customerId = null,
            int? accountId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int pageNumber = 1,
            int pageSize = 30)
        {
            var query = _context.CreditPayments
                .AsNoTracking()
                .Include(cp => cp.CreditAccount)
                    .ThenInclude(ca => ca!.Sale)
                .Include(cp => cp.Customer)
                .Include(cp => cp.User)
                .AsQueryable();

            if (customerId.HasValue && customerId.Value > 0)
                query = query.Where(cp => cp.CustomerId == customerId.Value);

            if (accountId.HasValue && accountId.Value > 0)
                query = query.Where(cp => cp.CreditAccountId == accountId.Value);

            if (startDate.HasValue)
                query = query.Where(cp => cp.CreatedAt >= startDate.Value.Date);

            if (endDate.HasValue)
            {
                var effectiveEnd = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(cp => cp.CreatedAt <= effectiveEnd);
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var payments = await query
                .OrderByDescending(cp => cp.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(cp => new CreditPaymentDto
                {
                    Id = cp.Id,
                    PaymentFolio = cp.PaymentFolio,
                    CreditAccountId = cp.CreditAccountId,
                    CreditAccountFolio = cp.CreditAccount != null ? cp.CreditAccount.Folio : string.Empty,
                    SaleId = cp.CreditAccount != null ? cp.CreditAccount.SaleId : 0,
                    SaleFolio = cp.CreditAccount != null && cp.CreditAccount.Sale != null ? cp.CreditAccount.Sale.Folio : string.Empty,
                    CustomerId = cp.CustomerId,
                    CustomerName = cp.Customer != null ? cp.Customer.Name : string.Empty,
                    Amount = cp.Amount,
                    PaymentMethod = cp.PaymentMethod,
                    PaymentReference = cp.PaymentReference,
                    Notes = cp.Notes,
                    ReceivedByUserName = cp.User != null ? cp.User.Name : "Cajero",
                    CreatedAt = cp.CreatedAt
                })
                .ToListAsync();

            return new PagedResponse<CreditPaymentDto>
            {
                Data = payments,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };
        }

        // Helper para validar efectivo / referencia al abonar
        private static void ValidarMetodoYReferenciaAbono(string paymentMethod, string? paymentReference, decimal montoAbonar)
        {
            if (paymentMethod == "Tarjeta" || paymentMethod == "Transferencia")
            {
                if (string.IsNullOrWhiteSpace(paymentReference))
                {
                    throw new InvalidOperationException($"Para abonos con {paymentMethod}, es obligatorio ingresar el número de autorización o referencia.");
                }
            }
            else if (paymentMethod == "Efectivo" && !string.IsNullOrWhiteSpace(paymentReference))
            {
                if (decimal.TryParse(paymentReference, out decimal montoRecibido))
                {
                    if (montoRecibido < montoAbonar)
                    {
                        throw new InvalidOperationException($"El efectivo recibido (${montoRecibido:N2}) es menor al abono que deseas registrar (${montoAbonar:N2}).");
                    }
                }
            }
        }

        // Helper interno/reutilizable para mapear una lista de CreditAccount con todos sus detalles de venta, devoluciones y abonos
        internal async Task<List<CreditAccountDetailDto>> MapAccountsToDetailDtosAsync(List<CreditAccount> accounts)
        {
            if (!accounts.Any()) return new List<CreditAccountDetailDto>();

            var todayStart = DateTime.Now.Date;
            var saleIds = accounts.Select(a => a.SaleId).Distinct().ToList();

            // Traemos todas las devoluciones ligadas a esos tickets en una sola consulta eficiente
            var returnsForSales = await _context.Returns
                .AsNoTracking()
                .Include(r => r.User)
                .Include(r => r.Details)
                    .ThenInclude(rd => rd.SaleDetail)
                .Where(r => saleIds.Contains(r.SaleId))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var returnsBySaleId = returnsForSales
                .GroupBy(r => r.SaleId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Mapa de cantidades y montos devueltos por cada SaleDetailId
            var returnedBySaleDetailId = returnsForSales
                .SelectMany(r => r.Details)
                .GroupBy(rd => rd.SaleDetailId)
                .ToDictionary(
                    g => g.Key,
                    g => (
                        QtyReturned: g.Sum(x => x.QuantityReturned),
                        AmountReturned: g.Sum(x => x.RefundAmount)
                    )
                );

            var result = new List<CreditAccountDetailDto>(accounts.Count);

            foreach (var acc in accounts)
            {
                var saleDetails = acc.Sale?.Details ?? new List<SaleDetail>();

                var itemsDto = saleDetails.Select(sd =>
                {
                    returnedBySaleDetailId.TryGetValue(sd.Id, out var retInfo);
                    decimal qtyRet = retInfo.QtyReturned;
                    decimal amtRet = retInfo.AmountReturned;

                    return new CreditAccountSaleItemDto
                    {
                        SaleDetailId = sd.Id,
                        ProductId = sd.ProductId,
                        PresentationId = sd.PresentationId,
                        ProductName = sd.ProductName,
                        Brand = sd.Brand,
                        QuantityTaken = sd.Quantity,
                        QuantityReturned = qtyRet,
                        NetQuantityKept = Math.Max(0m, sd.Quantity - qtyRet),
                        UnitPrice = sd.UnitPrice,
                        DiscountAmount = sd.DiscountAmount,
                        OriginalSubtotal = sd.Subtotal,
                        RefundedSubtotal = amtRet,
                        NetSubtotal = Math.Max(0m, sd.Subtotal - amtRet)
                    };
                }).ToList();

                returnsBySaleId.TryGetValue(acc.SaleId, out var saleReturns);
                var returnsDto = (saleReturns ?? new List<SaleReturn>()).Select(sr => new CreditAccountReturnDto
                {
                    ReturnId = sr.Id,
                    ReturnFolio = sr.ReturnFolio,
                    CreatedAt = sr.CreatedAt,
                    UserName = sr.User?.Name ?? "Usuario",
                    Reason = sr.Reason,
                    TotalRefunded = sr.TotalRefunded,
                    AppliedToDebtAmount = sr.AppliedToDebtAmount,
                    CashRefundedAmount = sr.CashRefundedAmount,
                    Items = sr.Details.Select(rd => new CreditAccountReturnItemDto
                    {
                        SaleDetailId = rd.SaleDetailId,
                        ProductName = rd.SaleDetail?.ProductName ?? "Producto",
                        QuantityReturned = rd.QuantityReturned,
                        RefundAmount = rd.RefundAmount
                    }).ToList()
                }).ToList();

                var paymentsDto = (acc.Payments ?? new List<CreditPayment>())
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new CreditPaymentDto
                    {
                        Id = p.Id,
                        PaymentFolio = p.PaymentFolio,
                        CreditAccountId = acc.Id,
                        CreditAccountFolio = acc.Folio,
                        SaleId = acc.SaleId,
                        SaleFolio = acc.Sale?.Folio ?? string.Empty,
                        CustomerId = acc.CustomerId,
                        CustomerName = acc.Customer?.Name ?? string.Empty,
                        Amount = p.Amount,
                        PaymentMethod = p.PaymentMethod,
                        PaymentReference = p.PaymentReference,
                        Notes = p.Notes,
                        ReceivedByUserName = p.User?.Name ?? "Cajero",
                        CreatedAt = p.CreatedAt
                    })
                    .ToList();

                bool isOverdue = acc.Balance > 0 && acc.DueDate.HasValue && acc.DueDate.Value.Date < todayStart;

                result.Add(new CreditAccountDetailDto
                {
                    Id = acc.Id,
                    Folio = acc.Folio,
                    Status = acc.Status,
                    IsOverdue = isOverdue,
                    CustomerId = acc.CustomerId,
                    CustomerName = acc.Customer?.Name ?? string.Empty,
                    CustomerAlias = acc.Customer?.Alias,
                    CustomerPhone = acc.Customer?.Phone,
                    CustomerSecondaryPhone = acc.Customer?.SecondaryPhone,
                    CustomerAddress = acc.Customer?.Address,
                    SaleId = acc.SaleId,
                    SaleFolio = acc.Sale?.Folio ?? string.Empty,
                    SaleDate = acc.Sale?.CreatedAt ?? acc.CreatedAt,
                    SoldByUserName = acc.User?.Name ?? "Cajero",
                    DiscountPercent = acc.Sale?.DiscountPercent ?? 0m,
                    DiscountAmount = acc.Sale?.DiscountAmount ?? 0m,
                    OriginalAmount = acc.OriginalAmount,
                    ReturnedAmount = acc.ReturnedAmount,
                    PaidAmount = acc.PaidAmount,
                    Balance = acc.Balance,
                    DueDate = acc.DueDate,
                    Notes = acc.Notes,
                    CreatedAt = acc.CreatedAt,
                    UpdatedAt = acc.UpdatedAt,
                    PaidAt = acc.PaidAt,
                    TotalItemsCount = itemsDto.Count,
                    Items = itemsDto,
                    Payments = paymentsDto,
                    Returns = returnsDto
                });
            }

            return result;
        }
    }
}
