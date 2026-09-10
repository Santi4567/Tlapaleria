using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Enums;
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class ExpenseService : IExpenseService
    {
        private readonly TlapaleriaContext _context;

        public ExpenseService(TlapaleriaContext context)
        {
            _context = context;
        }

        // ==============================================================================
        // 1. REGISTRAR UNA DEUDA Y GENERAR SUS PLAZOS (Si aplica)
        // ==============================================================================
        public async Task<AccountsPayable> CreateAccountsPayableAsync(CreateAccountsPayableDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Validar que el proveedor existe
                bool existeProveedor = await _context.Suppliers.AnyAsync(s => s.Id == dto.SupplierId && s.IsActive);
                if (!existeProveedor)
                    throw new Exception("El proveedor no existe o está inactivo.");

                // 2. Crear la cuenta por pagar
                var nuevaDeuda = new AccountsPayable
                {
                    SupplierId = dto.SupplierId,
                    Concept = dto.Concept,
                    TotalAmount = dto.TotalAmount,
                    Balance = dto.TotalAmount, // Al inicio, se debe todo
                    DueDate = dto.DueDate,
                    PaymentFrequencyDays = dto.PaymentFrequencyDays,
                    Status = AccountsPayableStatus.Pendiente.ToString()
                };

                _context.AccountsPayables.Add(nuevaDeuda);
                await _context.SaveChangesAsync(); // Se guarda para obtener el Id

                // 3. GENERADOR DE PLAN DE PAGOS (Si el usuario eligió pagar a plazos)
                if (dto.PaymentFrequencyDays.HasValue && dto.PaymentFrequencyDays.Value > 0)
                {
                    if (!dto.DueDate.HasValue)
                        throw new Exception("Si defines una frecuencia de pago, debes definir una Fecha Límite (DueDate) para calcular los plazos.");

                    // Calcular cuántos días tenemos en total para pagar
                    int diasTotales = (dto.DueDate.Value.Date - DateTime.Now.Date).Days;
                    if (diasTotales <= 0)
                        throw new Exception("La fecha límite debe ser en el futuro.");

                    // Calcular cantidad de cuotas (Redondeando hacia arriba)
                    int numeroDeCuotas = (int)Math.Ceiling((double)diasTotales / dto.PaymentFrequencyDays.Value);

                    // Calcular el monto sugerido por cuota
                    decimal montoSugerido = Math.Round(dto.TotalAmount / numeroDeCuotas, 2);

                    decimal sumaParcial = 0;
                    DateTime fechaCuotaActual = DateTime.Now.Date;

                    for (int i = 1; i <= numeroDeCuotas; i++)
                    {
                        fechaCuotaActual = fechaCuotaActual.AddDays(dto.PaymentFrequencyDays.Value);

                        // Ajustar la última cuota para que no haya diferencias por el redondeo de centavos
                        decimal montoFinal = (i == numeroDeCuotas) ? (dto.TotalAmount - sumaParcial) : montoSugerido;
                        sumaParcial += montoFinal;

                        var cuota = new PaymentSchedule
                        {
                            AccountsPayableId = nuevaDeuda.Id,
                            InstallmentNumber = i,
                            DueDate = fechaCuotaActual,
                            Amount = montoFinal,
                            Status = PaymentScheduleStatus.Pendiente.ToString()
                        };

                        _context.PaymentSchedules.Add(cuota);
                        await _context.SaveChangesAsync();

                        // 4. Generar el Recordatorio Automático (Ej: avisar 1 día antes)
                        var recordatorio = new PaymentReminder
                        {
                            ScheduleId = cuota.Id,
                            RemindAt = cuota.DueDate.AddDays(-1), // Aviso un día antes
                            Channel = ReminderChannel.InApp.ToString()
                        };
                        _context.PaymentReminders.Add(recordatorio);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return nuevaDeuda;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ==============================================================================
        // 2. REGISTRAR UN EGRESO (El Trigger de la BD hace el resto)
        // ==============================================================================
        public async Task<Expense> CreateExpenseAsync(CreateExpenseDto dto, int userId)
        {
            // 1. Validaciones básicas
            bool existeCategoria = await _context.ExpenseCategories.AnyAsync(c => c.Id == dto.CategoryId && c.IsActive);
            if (!existeCategoria) throw new Exception("La categoría de gasto no existe.");

            if (dto.SupplierId.HasValue)
            {
                bool existeProveedor = await _context.Suppliers.AnyAsync(s => s.Id == dto.SupplierId.Value && s.IsActive);
                if (!existeProveedor) throw new Exception("El proveedor no existe.");
            }

            // 2. Armar el objeto
            var nuevoEgreso = new Expense
            {
                Concept = dto.Concept,
                Amount = dto.Amount,
                PaymentMethod = dto.PaymentMethod,
                CategoryId = dto.CategoryId,
                SupplierId = dto.SupplierId,
                AccountsPayableId = dto.AccountsPayableId,
                ScheduleId = dto.ScheduleId,
                ReceiptNumber = dto.ReceiptNumber,
                ReceiptUrl = dto.ReceiptUrl,
                UserId = userId,
                ExpenseDate = DateTime.Now
            };

            _context.Expenses.Add(nuevoEgreso);

            try
            {
                // ¡AQUÍ OCURRE LA MAGIA!
                // Al hacer SaveChanges, Entity Framework lanza el INSERT a MySQL.
                // Si el pago excede la deuda, el Trigger lanza un SIGNAL SQLSTATE '45000'.
                // EF Core atrapa ese error y lo convierte en una DbUpdateException.
                await _context.SaveChangesAsync();
                return nuevoEgreso;
            }
            catch (DbUpdateException ex)
            {
                // Si el error viene de nuestro Trigger (45000), el InnerException trae el mensaje exacto
                if (ex.InnerException != null && ex.InnerException.Message.Contains("El abono excede el saldo"))
                {
                    throw new Exception(ex.InnerException.Message);
                }

                throw new Exception("Error al registrar el egreso en la base de datos.");
            }
        }

        // ==============================================================================
        // 3. ANULAR UN EGRESO (El Trigger de la BD hace la reversión)
        // ==============================================================================
        public async Task<bool> CancelExpenseAsync(int id, int userId)
        {
            var egreso = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id);

            if (egreso == null)
                throw new Exception("El egreso no existe.");

            if (!egreso.IsActive)
                throw new Exception("Este egreso ya fue anulado anteriormente.");

            // Desactivamos lógicamente.
            egreso.IsActive = false;

            try
            {
                // Al hacer SaveChanges, el Trigger "trg_expense_reversal" se ejecuta.
                // Restaurará el balance, los status de la deuda y las cuotas.
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex)
            {
                // Captura si violamos alguna regla de inmutabilidad (aunque aquí solo tocamos IsActive)
                if (ex.InnerException != null)
                    throw new Exception(ex.InnerException.Message);

                throw new Exception("Error al anular el egreso.");
            }
        }

        // ==============================================================================
        // 4. OBTENER DEUDA POR ID (Para pintar el UI)
        // ==============================================================================
        public async Task<AccountsPayable> GetAccountsPayableByIdAsync(int id)
        {
            var deuda = await _context.AccountsPayables
                .Include(a => a.Supplier)
                .Include(a => a.PaymentSchedules)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (deuda == null)
                throw new Exception("La cuenta por pagar no existe.");

            return deuda;
        }
        // ==============================================================================
        // 5. OBTENER TODOS LOS REGISTROS DE LA TABLA accountspayable
        // ==============================================================================

        public async Task<PagedResponse<AccountsPayable>> GetAccountsPayableAsync(int pageNumber, int pageSize, bool? isActive, DateTime? startDate, DateTime? endDate)
        {
            var query = _context.AccountsPayables
                .Include(a => a.Supplier)
                .AsQueryable();

            // Filtro de 3 estados (null = todos)
            if (isActive.HasValue)
            {
                query = query.Where(a => a.IsActive == isActive.Value);
            }

            // Filtro de fechas por creación de la deuda
            if (startDate.HasValue)
            {
                var start = startDate.Value.Date;

                if (endDate.HasValue)
                {
                    var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(a => a.CreatedAt >= start && a.CreatedAt <= end);
                }
                else
                {
                    // Si no mandan fecha fin, buscamos solo el día del startDate
                    var end = start.AddDays(1).AddTicks(-1);
                    query = query.Where(a => a.CreatedAt >= start && a.CreatedAt <= end);
                }
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var deudas = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResponse<AccountsPayable>
            {
                Data = deudas,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };
        }

        // ==============================================================================
        // 6. OBTENER TODO LOS REGISTROS DE LA TABLA EXPENSES
        // ==============================================================================

        public async Task<PagedResponse<Expense>> GetExpensesAsync(int pageNumber, int pageSize, bool? isActive, DateTime? startDate, DateTime? endDate)
        {
            var query = _context.Expenses
                .Include(e => e.Category)
                .Include(e => e.Supplier)
                .AsQueryable();

            // Filtro de 3 estados
            if (isActive.HasValue)
            {
                query = query.Where(e => e.IsActive == isActive.Value);
            }

            // Filtro de fechas inteligente
            if (startDate.HasValue)
            {
                var start = startDate.Value.Date;

                if (endDate.HasValue)
                {
                    var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(e => e.ExpenseDate >= start && e.ExpenseDate <= end);
                }
                else
                {
                    // Si no mandan fecha fin, buscamos solo el día del startDate
                    var end = start.AddDays(1).AddTicks(-1);
                    query = query.Where(e => e.ExpenseDate >= start && e.ExpenseDate <= end);
                }
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var egresos = await query
                .OrderByDescending(e => e.ExpenseDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResponse<Expense>
            {
                Data = egresos,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };
        }

        // ==============================================================================
        // 5. OBTENER CATEGORÍAS (Para el Dropdown del Frontend)
        // ==============================================================================
        public async Task<List<ExpenseCategory>> GetExpenseCategoriesAsync(bool? isActive = true)
        {
            var query = _context.ExpenseCategories.AsQueryable();

            // Filtro estricto de 3 estados (true, false, o null para todos)
            if (isActive.HasValue)
            {
                query = query.Where(c => c.IsActive == isActive.Value);
            }

            return await query.OrderBy(c => c.Name).ToListAsync();
        }

        // ==============================================================================
        // 7. OBTENER PLAZOS (Para el Calendario de Pagos)
        // ==============================================================================
        public async Task<PagedResponse<PaymentSchedule>> GetPaymentSchedulesAsync(int pageNumber, int pageSize, int? accountsPayableId, string? status, DateTime? startDate, DateTime? endDate)
        {
            var query = _context.PaymentSchedules
                .Include(p => p.AccountsPayable)
                .ThenInclude(a => a.Supplier) // Traemos al proveedor para saber a quién le debemos
                .AsQueryable();

            if (accountsPayableId.HasValue)
                query = query.Where(p => p.AccountsPayableId == accountsPayableId.Value);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.Status == status);

            if (startDate.HasValue)
            {
                var start = startDate.Value.Date;
                if (endDate.HasValue)
                {
                    var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(p => p.DueDate >= start && p.DueDate <= end);
                }
                else
                {
                    var end = start.AddDays(1).AddTicks(-1);
                    query = query.Where(p => p.DueDate >= start && p.DueDate <= end);
                }
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var plazos = await query
                .OrderBy(p => p.DueDate) // Ordenamos por los más próximos a vencer
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResponse<PaymentSchedule>
            {
                Data = plazos,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };
        }

        // ==============================================================================
        // 8. OBTENER RECORDATORIOS PENDIENTES (Para la campanita de notificaciones)
        // ==============================================================================
        public async Task<List<PaymentReminder>> GetPendingRemindersAsync()
        {
            // Buscamos recordatorios pendientes cuya fecha de aviso sea hoy o ya haya pasado
            // (Le damos un margen de buscar hasta 3 días en el futuro por si quieren ver "próximos")
            DateTime limiteAlerta = DateTime.Now.AddDays(3);

            return await _context.PaymentReminders
                .Include(r => r.Schedule)
                .ThenInclude(s => s.AccountsPayable)
                .ThenInclude(a => a.Supplier)
                .Where(r => r.Status == ReminderStatus.Pendiente.ToString() && r.RemindAt <= limiteAlerta)
                .OrderBy(r => r.RemindAt)
                .ToListAsync();
        }
    }
}