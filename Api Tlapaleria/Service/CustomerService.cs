using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Enums;
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly TlapaleriaContext _context;
        private readonly ICreditAccountService _creditAccountService;

        public CustomerService(TlapaleriaContext context, ICreditAccountService creditAccountService)
        {
            _context = context;
            _creditAccountService = creditAccountService;
        }

        // Obtener lista paginada de clientes con su resumen de deuda actual
        public async Task<PagedResponse<CustomerSummaryDto>> GetAllAsync(
            bool isActive = true,
            bool onlyWithDebt = false,
            string? search = null,
            int pageNumber = 1,
            int pageSize = 20)
        {
            var query = _context.Customers
                .AsNoTracking()
                .Where(c => c.IsActive == isActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                query = query.Where(c =>
                    c.Name.Contains(term) ||
                    (c.Alias != null && c.Alias.Contains(term)) ||
                    (c.Phone != null && c.Phone.Contains(term)) ||
                    (c.SecondaryPhone != null && c.SecondaryPhone.Contains(term)) ||
                    (c.Address != null && c.Address.Contains(term))
                );
            }

            if (onlyWithDebt)
            {
                query = query.Where(c => _context.CreditAccounts.Any(ca =>
                    ca.CustomerId == c.Id &&
                    ca.Balance > 0 &&
                    ca.Status != CreditAccountStatus.Cancelado.ToString()));
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var customers = await query
                .OrderBy(c => c.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var summaryDtos = await MapCustomersToSummaryDtosAsync(customers);

            return new PagedResponse<CustomerSummaryDto>
            {
                Data = summaryDtos,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };
        }

        // Búsqueda rápida de clientes (para el selector en mostrador al vender a crédito)
        public async Task<List<CustomerSummaryDto>> SearchAsync(string termino, bool isActive = true)
        {
            string term = termino?.Trim() ?? string.Empty;

            var customers = await _context.Customers
                .AsNoTracking()
                .Where(c => c.IsActive == isActive && (
                    c.Name.Contains(term) ||
                    (c.Alias != null && c.Alias.Contains(term)) ||
                    (c.Phone != null && c.Phone.Contains(term)) ||
                    (c.SecondaryPhone != null && c.SecondaryPhone.Contains(term)) ||
                    (c.Address != null && c.Address.Contains(term))
                ))
                .OrderBy(c => c.Name)
                .Take(25)
                .ToListAsync();

            return await MapCustomersToSummaryDtosAsync(customers);
        }

        // Obtener resumen de un cliente por ID
        public async Task<CustomerSummaryDto> GetByIdAsync(int id)
        {
            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null)
                throw new KeyNotFoundException($"El cliente con ID {id} no fue encontrado.");

            var mapped = await MapCustomersToSummaryDtosAsync(new List<Customer> { customer });
            return mapped.First();
        }

        // Expediente completo del cliente: contacto, deuda total, todas sus cuentas (con qué se llevó y cuándo) y sus abonos
        public async Task<CustomerFullProfileDto> GetCustomerFullProfileAsync(int customerId)
        {
            var customerSummary = await GetByIdAsync(customerId);

            // Todas las cuentas del cliente (primero las que tienen deuda pendiente, luego el historial)
            var accounts = await _context.CreditAccounts
                .AsNoTracking()
                .Include(ca => ca.Customer)
                .Include(ca => ca.User)
                .Include(ca => ca.Sale)
                    .ThenInclude(s => s!.Details)
                .Include(ca => ca.Payments)
                    .ThenInclude(p => p.User)
                .Where(ca => ca.CustomerId == customerId)
                .OrderByDescending(ca => ca.Balance > 0)
                .ThenByDescending(ca => ca.CreatedAt)
                .ToListAsync();

            List<CreditAccountDetailDto> accountDetails;
            if (_creditAccountService is CreditAccountService concreteService)
            {
                accountDetails = await concreteService.MapAccountsToDetailDtosAsync(accounts);
            }
            else
            {
                var paged = await _creditAccountService.GetAccountsAsync(customerId: customerId, pageNumber: 1, pageSize: 200);
                accountDetails = paged.Data;
            }

            var recentPayments = accountDetails
                .SelectMany(a => a.Payments)
                .OrderByDescending(p => p.CreatedAt)
                .Take(50)
                .ToList();

            var validAccounts = accountDetails
                .Where(a => a.Status != CreditAccountStatus.Cancelado.ToString())
                .ToList();

            return new CustomerFullProfileDto
            {
                Customer = customerSummary,
                TotalHistoricalPurchased = validAccounts.Sum(a => a.OriginalAmount),
                TotalHistoricalReturned = accountDetails.Sum(a => a.ReturnedAmount),
                TotalHistoricalPaid = accountDetails.Sum(a => a.PaidAmount),
                TotalCurrentDebt = accountDetails.Sum(a => a.Balance),
                Accounts = accountDetails,
                RecentPayments = recentPayments
            };
        }

        // Crear un nuevo cliente de confianza
        public async Task<Customer> CreateAsync(CreateCustomerDto dto)
        {
            string nombreLimpio = dto.Name.Trim();

            bool yaExiste = await _context.Customers
                .AnyAsync(c => c.Name.ToLower() == nombreLimpio.ToLower());

            if (yaExiste)
                throw new InvalidOperationException($"Ya existe un cliente registrado con el nombre '{nombreLimpio}'.");

            var nuevoCliente = new Customer
            {
                Name = nombreLimpio,
                Alias = dto.Alias?.Trim(),
                Phone = dto.Phone?.Trim(),
                SecondaryPhone = dto.SecondaryPhone?.Trim(),
                Email = dto.Email?.Trim(),
                Address = dto.Address?.Trim(),
                Notes = dto.Notes?.Trim(),
                CreditLimit = dto.CreditLimit,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Customers.Add(nuevoCliente);
            await _context.SaveChangesAsync();

            return nuevoCliente;
        }

        // Actualizar datos de contacto o límite de crédito de un cliente
        public async Task<Customer> UpdateAsync(int id, UpdateCustomerDto dto)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
                throw new KeyNotFoundException($"El cliente con ID {id} no fue encontrado.");

            string nombreLimpio = dto.Name.Trim();

            bool duplicado = await _context.Customers
                .AnyAsync(c => c.Id != id && c.Name.ToLower() == nombreLimpio.ToLower());

            if (duplicado)
                throw new InvalidOperationException($"Ya existe otro cliente con el nombre '{nombreLimpio}'.");

            // Si quieren desactivar al cliente, verificamos que no deba dinero actualmente
            if (!dto.IsActive && customer.IsActive)
            {
                decimal deudaPendiente = await _context.CreditAccounts
                    .Where(ca => ca.CustomerId == id && ca.Balance > 0 && ca.Status != CreditAccountStatus.Cancelado.ToString())
                    .SumAsync(ca => (decimal?)ca.Balance) ?? 0m;

                if (deudaPendiente > 0)
                {
                    throw new InvalidOperationException(
                        $"No se puede desactivar al cliente '{customer.Name}' porque aún tiene una deuda pendiente de ${deudaPendiente:N2}.");
                }
            }

            customer.Name = nombreLimpio;
            customer.Alias = dto.Alias?.Trim();
            customer.Phone = dto.Phone?.Trim();
            customer.SecondaryPhone = dto.SecondaryPhone?.Trim();
            customer.Email = dto.Email?.Trim();
            customer.Address = dto.Address?.Trim();
            customer.Notes = dto.Notes?.Trim();
            customer.CreditLimit = dto.CreditLimit;
            customer.IsActive = dto.IsActive;
            customer.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return customer;
        }

        // Desactivar (Soft Delete) un cliente validando primero que no tenga deuda activa
        public async Task DeleteAsync(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
                throw new KeyNotFoundException($"El cliente con ID {id} no fue encontrado.");

            decimal deudaPendiente = await _context.CreditAccounts
                .Where(ca => ca.CustomerId == id && ca.Balance > 0 && ca.Status != CreditAccountStatus.Cancelado.ToString())
                .SumAsync(ca => (decimal?)ca.Balance) ?? 0m;

            if (deudaPendiente > 0)
            {
                throw new InvalidOperationException(
                    $"No se puede desactivar al cliente '{customer.Name}' porque aún debe ${deudaPendiente:N2}. Liquida sus cuentas primero.");
            }

            customer.IsActive = false;
            customer.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
        }

        // Helper para calcular saldos y estadísticas de una lista de clientes en una sola consulta agrupada
        private async Task<List<CustomerSummaryDto>> MapCustomersToSummaryDtosAsync(List<Customer> customers)
        {
            if (!customers.Any()) return new List<CustomerSummaryDto>();

            var todayStart = DateTime.Now.Date;
            var customerIds = customers.Select(c => c.Id).ToList();

            var rawAccounts = await _context.CreditAccounts
                .AsNoTracking()
                .Where(ca => customerIds.Contains(ca.CustomerId) && ca.Status != CreditAccountStatus.Cancelado.ToString())
                .Select(ca => new
                {
                    ca.CustomerId,
                    ca.Balance,
                    ca.DueDate,
                    ca.CreatedAt
                })
                .ToListAsync();

            var lastPaymentsByCustomer = await _context.CreditPayments
                .AsNoTracking()
                .Where(cp => customerIds.Contains(cp.CustomerId))
                .GroupBy(cp => cp.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    LastPaymentDate = g.Max(x => (DateTime?)x.CreatedAt)
                })
                .ToDictionaryAsync(x => x.CustomerId, x => x.LastPaymentDate);

            var accountsByCustomer = rawAccounts
                .GroupBy(a => a.CustomerId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return customers.Select(c =>
            {
                accountsByCustomer.TryGetValue(c.Id, out var custAccounts);
                custAccounts ??= new();

                var activeList = custAccounts.Where(a => a.Balance > 0).ToList();
                decimal currentDebt = activeList.Sum(a => a.Balance);
                int activeCount = activeList.Count;
                int overdueCount = activeList.Count(a => a.DueDate.HasValue && a.DueDate.Value.Date < todayStart);
                DateTime? lastPurchase = custAccounts.Any() ? custAccounts.Max(a => a.CreatedAt) : null;

                lastPaymentsByCustomer.TryGetValue(c.Id, out var lastPayment);

                decimal? availableCredit = null;
                if (c.CreditLimit.HasValue && c.CreditLimit.Value > 0)
                {
                    availableCredit = Math.Max(0m, c.CreditLimit.Value - currentDebt);
                }

                return new CustomerSummaryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Alias = c.Alias,
                    Phone = c.Phone,
                    SecondaryPhone = c.SecondaryPhone,
                    Email = c.Email,
                    Address = c.Address,
                    Notes = c.Notes,
                    CreditLimit = c.CreditLimit,
                    AvailableCredit = availableCredit,
                    CurrentDebt = currentDebt,
                    ActiveAccountsCount = activeCount,
                    OverdueAccountsCount = overdueCount,
                    TotalHistoricalAccountsCount = custAccounts.Count,
                    LastPurchaseDate = lastPurchase,
                    LastPaymentDate = lastPayment,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt
                };
            }).ToList();
        }
    }
}
