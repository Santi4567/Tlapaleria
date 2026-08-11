using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Api_Tlapaleria.Enums; // No olvides tu nuevo namespace de enums
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class PendingOrderService : IPendingOrderService
    {
        private readonly TlapaleriaContext _context;

        public PendingOrderService(TlapaleriaContext context)
        {
            _context = context;
        }

        //POST: Crea un nuevo registro en la tabla de Pendientes
        // POST: Crea un nuevo registro en la tabla de Pendientes
        public async Task<PendingOrder> CreatePendingOrderAsync(CreatePendingOrderDto datos, int userId)
        {
            // ==========================================
            // 1. VALIDACIÓN DE PRODUCTO NUEVO VS CATÁLOGO
            // ==========================================
            if (!datos.ProductId.HasValue && string.IsNullOrWhiteSpace(datos.NewProductName))
                throw new Exception("Debes seleccionar un producto del catálogo o escribir el nombre del producto nuevo.");

            if (datos.ProductId.HasValue && !string.IsNullOrWhiteSpace(datos.NewProductName))
                throw new Exception("No puedes enviar un producto del catálogo y un nombre nuevo al mismo tiempo.");

            // Si mandaron ProductId, validamos que exista
            if (datos.ProductId.HasValue)
            {
                var producto = await _context.Products.FirstOrDefaultAsync(p => p.Id == datos.ProductId && p.IsActive);
                if (producto == null) throw new Exception("El producto seleccionado no existe o se encuentra inactivo.");

                // Validamos duplicados SOLO si es un producto de catálogo
                var pedidoExistente = await _context.PendingOrders
                    .FirstOrDefaultAsync(po => po.ProductId == datos.ProductId && po.Status == PendingOrderStatus.Pendiente);
                if (pedidoExistente != null) throw new Exception("Este producto ya está anotado en la libreta como Pendiente.");
            }

            if (datos.SupplierId.HasValue)
            {
                var proveedor = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == datos.SupplierId.Value && s.IsActive);
                if (proveedor == null) throw new Exception("El proveedor seleccionado no existe o se encuentra inactivo.");
            }

            var usuario = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (usuario == null) throw new Exception("El usuario autenticado no es válido o está inactivo.");

            // ==========================================
            // 2. CREACIÓN DEL PEDIDO
            // ==========================================
            var nuevoPedido = new PendingOrder
            {
                ProductId = datos.ProductId,
                NewProductName = datos.NewProductName?.Trim(), // Guardamos el texto libre
                SupplierId = datos.SupplierId,
                UserId = userId,
                QuantityText = datos.QuantityText,
                Notes = datos.Notes,
                Status = PendingOrderStatus.Pendiente,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.PendingOrders.Add(nuevoPedido);

            // ==========================================
            // 3. REGISTRO EN EL HISTORIAL (Caja Negra)
            // ==========================================
            var historial = new PendingOrderHistory
            {
                PendingOrder = nuevoPedido, // EF Core asignará el ID automáticamente tras guardar
                Status = PendingOrderStatus.Pendiente,
                UserId = userId,
                CreatedAt = DateTime.Now
            };

            // Asumiendo que agregaste el DbSet<PendingOrderHistory> al TlapaleriaContext:
            _context.PendingOrderHistories.Add(historial);

            await _context.SaveChangesAsync();

            // Recargamos datos para devolver un JSON completo
            if (nuevoPedido.ProductId.HasValue)
                await _context.Entry(nuevoPedido).Reference(p => p.Product).LoadAsync();

            if (nuevoPedido.SupplierId.HasValue)
                await _context.Entry(nuevoPedido).Reference(p => p.Supplier).LoadAsync();

            return nuevoPedido;
        }

        // Actualizar el estado del producto
        public async Task<PendingOrder> UpdatePendingOrderStatusAsync(int id, PendingOrderStatus status, int userId)
        {
            var pedidoExistente = await _context.PendingOrders
                .Include(po => po.Product)
                .Include(po => po.Supplier)
                .FirstOrDefaultAsync(po => po.Id == id);

            if (pedidoExistente == null)
                throw new Exception($"El pedido con ID {id} no existe.");

            // ==========================================
            // 1. REGLA PARA PRODUCTOS NUEVOS
            // ==========================================
            // Si intentan COMPLETAR el pedido, pero no tiene ProductId (era producto nuevo), bloqueamos.
            if (status == PendingOrderStatus.Completado && !pedidoExistente.ProductId.HasValue)
            {
                throw new Exception($"Antes de marcar como 'Completado', debes dar de alta el producto '{pedidoExistente.NewProductName}' en el catálogo y asignarle su ID.");
            }

            var usuario = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (usuario == null) throw new Exception("El usuario autenticado no es válido.");

            // ==========================================
            // 2. CAMBIO DE ESTADO
            // ==========================================
            pedidoExistente.Status = status;
            pedidoExistente.UserId = userId;
            pedidoExistente.UpdatedAt = DateTime.Now;

            // ==========================================
            // 3. REGISTRO EN EL HISTORIAL (Caja Negra)
            // ==========================================
            var historial = new PendingOrderHistory
            {
                PendingOrderId = pedidoExistente.Id,
                Status = status,
                UserId = userId,
                CreatedAt = DateTime.Now
            };
            _context.PendingOrderHistories.Add(historial);

            await _context.SaveChangesAsync();

            return pedidoExistente;
        }

        //Endpoint maestro
        public async Task<PagedResponse<PendingOrder>> GetAdvancedPendingOrdersAsync(
            string? search = null,
            int? supplierId = null,
            int? productId = null,
            PendingOrderStatus? status = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int pageNumber = 1,
            int pageSize = 50)
        {
            // --- LÍMITES Y SEGURIDAD ---
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var query = _context.PendingOrders
                .AsNoTracking()
                .Include(po => po.Product)
                .Include(po => po.Supplier)
                .Include(po => po.User)
                .AsQueryable();

            // 1. Filtro: Buscador de texto
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower().Trim();
                query = query.Where(po =>
                    (po.Product != null && (
                        po.Product.Name.ToLower().Contains(term) ||
                        po.Product.InternalCode.ToLower().Contains(term) ||
                        po.Product.Barcode == term
                    )) ||
                    (po.NewProductName != null && po.NewProductName.ToLower().Contains(term)) // <-- Busca también en los nuevos
                );
            }

            // 2. Filtro: Proveedor (Meta 4)
            if (supplierId.HasValue)
            {
                if (supplierId.Value == 0) // Si mandan 0, asumimos que quieren los que NO tienen proveedor
                    query = query.Where(po => po.SupplierId == null);
                else
                    query = query.Where(po => po.SupplierId == supplierId.Value);
            }

            // 3. Filtro: Producto específico (Meta 3)
            if (productId.HasValue && productId.Value > 0)
            {
                query = query.Where(po => po.ProductId == productId.Value);
            }

            // 4. Filtro: Estado (Meta 1 y 2)
            if (status.HasValue)
            {
                query = query.Where(po => po.Status == status.Value);
            }

            // 5. Filtro: Rango de Fechas
            if (startDate.HasValue)
            {
                query = query.Where(po => po.CreatedAt >= startDate.Value.Date);
            }
            if (endDate.HasValue)
            {
                var finalDelDia = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(po => po.CreatedAt <= finalDelDia);
            }

            // --- PAGINACIÓN Y EJECUCIÓN ---
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var resultados = await query
                .OrderByDescending(po => po.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResponse<PendingOrder>
            {
                Data = resultados,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };
        }

        //Buscador por ID 
        public async Task<PendingOrder> GetPendingOrderByIdAsync(int id)
        {
            var pedido = await _context.PendingOrders
                .Include(po => po.Product)
                .Include(po => po.Supplier)
                .Include(po => po.User) // Traemos quién lo anotó
                .FirstOrDefaultAsync(po => po.Id == id);

            if (pedido == null)
                throw new Exception($"El pedido pendiente con ID {id} no fue encontrado.");

            return pedido;
        }

        // ==============================================================
        // NUEVO MÉTODO: Filtros avanzados (Fechas, Producto, Completados)
        // ==============================================================
        public async Task<PagedResponse<PendingOrder>> GetAdvancedPendingOrdersAsync(
            bool excludeCompleted = false,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? productId = null,
            int pageNumber = 1,
            int pageSize = 50)
        {
            // --- LÍMITES Y SEGURIDAD ---
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100; // Permitir 100 si es para un reporte de fechas

            var query = _context.PendingOrders
                .AsNoTracking()
                .Include(po => po.Product)
                .Include(po => po.Supplier)
                .Include(po => po.User)
                .AsQueryable();

            // 1. Filtro: Excluir Completados (Trae Pendientes y Cancelados)
            if (excludeCompleted)
            {
                query = query.Where(po => po.Status != PendingOrderStatus.Completado);
            }
            // Si es false, simplemente trae TODOS por defecto

            // 2. Filtro: Rango de fechas o día específico
            if (startDate.HasValue)
            {
                // Comparamos desde las 00:00:00 del día
                query = query.Where(po => po.CreatedAt >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                // Comparamos hasta las 23:59:59 del día final
                var finalDelDia = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(po => po.CreatedAt <= finalDelDia);
            }

            // 3. Filtro: Historial de un producto en específico
            if (productId.HasValue && productId.Value > 0)
            {
                query = query.Where(po => po.ProductId == productId.Value);
            }

            // --- PAGINACIÓN ---
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var resultados = await query
                .OrderByDescending(po => po.CreatedAt) // Los más recientes primero
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResponse<PendingOrder>
            {
                Data = resultados,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };
        }

        //Actualizacion de Datos
        public async Task<PendingOrder> UpdatePendingOrderAsync(int id, UpdatePendingOrderDto datos, int userId)
        {
            // 1. Buscamos el pedido existente
            var pedidoExistente = await _context.PendingOrders
                .Include(po => po.Product)
                .Include(po => po.Supplier)
                .FirstOrDefaultAsync(po => po.Id == id);

            if (pedidoExistente == null)
                throw new Exception($"El pedido con ID {id} no existe.");

            // ==========================================
            // 2. REGLAS DE NEGOCIO ESTRICTAS
            // ==========================================

            // Regla A: Bloqueo total para estados definitivos de cierre
            if (pedidoExistente.Status == PendingOrderStatus.Cancelado ||
                pedidoExistente.Status == PendingOrderStatus.Completado)
            {
                throw new Exception("El pedido ya está cerrado (Cancelado o Completado). No se permiten modificaciones.");
            }

            // Regla B: Si el estado es "Pedido"
            if (pedidoExistente.Status == PendingOrderStatus.Pedido)
            {
                // Verificamos que no intenten cambiar la cantidad o el proveedor
                if (pedidoExistente.QuantityText != datos.QuantityText ||
                    pedidoExistente.SupplierId != datos.SupplierId)
                {
                    throw new Exception("El pedido ya fue enviado. En este estado únicamente está permitido actualizar las Notas o enlazar el Producto oficial.");
                }

                // Si pasan la validación, aplicamos solo las notas
                pedidoExistente.Notes = datos.Notes;
            }

            // Regla C: Si el estado es "Pendiente"
            else if (pedidoExistente.Status == PendingOrderStatus.Pendiente)
            {
                if (datos.SupplierId.HasValue && datos.SupplierId != pedidoExistente.SupplierId)
                {
                    var proveedor = await _context.Suppliers
                        .FirstOrDefaultAsync(s => s.Id == datos.SupplierId.Value && s.IsActive);

                    if (proveedor == null)
                        throw new Exception("El proveedor seleccionado no existe o se encuentra inactivo.");
                }

                // Aplicamos todos los cambios
                pedidoExistente.QuantityText = datos.QuantityText;
                pedidoExistente.SupplierId = datos.SupplierId;
                pedidoExistente.Notes = datos.Notes;
            }

            // ==========================================
            // 3. ENLACE DEL NUEVO PRODUCTO
            // ==========================================
            // Esto se ejecuta tanto para "Pendiente" como para "Pedido"
            if (datos.ProductId.HasValue && datos.ProductId != pedidoExistente.ProductId)
            {
                var producto = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == datos.ProductId.Value && p.IsActive);

                if (producto == null)
                    throw new Exception("El producto que intentas enlazar no existe o está inactivo en el catálogo principal.");

                // Inyectamos el ID oficial para que reemplace al producto temporal
                pedidoExistente.ProductId = datos.ProductId.Value;
            }

            // 4. Validar al usuario que realiza la acción
            var usuario = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (usuario == null)
                throw new Exception("El usuario autenticado no es válido o está inactivo.");

            // 5. Actualizamos los rastros de auditoría
            pedidoExistente.UserId = userId;
            pedidoExistente.UpdatedAt = DateTime.Now;

            // 6. Guardamos en base de datos
            await _context.SaveChangesAsync();

            // Recargamos las relaciones por si sufrieron cambios en este proceso
            if (pedidoExistente.SupplierId.HasValue)
                await _context.Entry(pedidoExistente).Reference(p => p.Supplier).LoadAsync();

            if (pedidoExistente.ProductId.HasValue)
                await _context.Entry(pedidoExistente).Reference(p => p.Product).LoadAsync();

            return pedidoExistente;
        }

        // GET: Obtiene la línea de tiempo de un pedido
        public async Task<List<PendingOrderHistoryDto>> GetPendingOrderHistoryAsync(int pendingOrderId)
        {
            var existePedido = await _context.PendingOrders.AnyAsync(po => po.Id == pendingOrderId);
            if (!existePedido)
                throw new Exception($"El pedido con ID {pendingOrderId} no existe.");

            var historial = await _context.PendingOrderHistories
                .Include(h => h.User) // Traemos la tabla users para obtener el nombre
                .Where(h => h.PendingOrderId == pendingOrderId)
                .OrderBy(h => h.CreatedAt) // Orden cronológico (del más viejo al más nuevo)
                .Select(h => new PendingOrderHistoryDto
                {
                    Id = h.Id,
                    PendingOrderId = h.PendingOrderId,
                    StatusName = h.Status.ToString(), // Convierte el Enum (0,1,2,3) a texto ("Pendiente", "Pedido", etc.)
                    UserName = h.User.Name, // Aquí usamos el Name de tu tabla de users
                    CreatedAt = h.CreatedAt
                })
                .ToListAsync();

            return historial;
        }

        //Recibir mercacia con filtros 
        public async Task<PendingOrder> ReceivePendingOrderAsync(int id, ReceivePendingOrderDto datos, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Buscamos el pedido con toda la cadena de relaciones necesaria
                var pedido = await _context.PendingOrders
                    .Include(po => po.Product)
                        .ThenInclude(p => p.Presentations)
                    .FirstOrDefaultAsync(po => po.Id == id);

                if (pedido == null)
                    throw new Exception($"El pedido con ID {id} no existe.");

                // ==========================================
                // REGLA ESTRICTA 1: SOLO SE PERMITE SI EL ESTADO ES "PEDIDO" (1)
                // ==========================================
                if (pedido.Status != PendingOrderStatus.Pedido)
                    throw new Exception($"Operación denegada. El pedido actual está en estado '{pedido.Status}'. La recepción de mercancía solo se permite cuando el pedido ya fue enviado al proveedor (Estado 1 - Pedido).");

                // ==========================================
                // ESCENARIO A: EL PROVEEDOR NO LO TRAJO (Agotado/Cancelado)
                // ==========================================
                if (datos.FinalStatus == PendingOrderStatus.Cancelado)
                {
                    pedido.Status = PendingOrderStatus.Cancelado;
                    pedido.UserId = userId;
                    pedido.UpdatedAt = DateTime.Now;

                    _context.PendingOrderHistories.Add(new PendingOrderHistory
                    {
                        PendingOrderId = pedido.Id,
                        Status = PendingOrderStatus.Cancelado,
                        UserId = userId,
                        CreatedAt = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return pedido;
                }

                // ==========================================
                // ESCENARIO B: LA MERCANCÍA SÍ LLEGÓ (Entrada)
                // ==========================================
                if (datos.FinalStatus == PendingOrderStatus.Completado)
                {
                    if (!pedido.ProductId.HasValue || pedido.Product == null)
                        throw new Exception("Antes de recibir y sumar mercancía, debes dar de alta el producto nuevo y enlazarle su ID oficial.");

                    if (!pedido.Product.IsActive)
                        throw new Exception("No puedes recibir mercancía ni actualizar precios de un producto inactivo.");

                    // ==========================================
                    // REGLA ESTRICTA 2: INVENTARIO OBLIGATORIO
                    // ==========================================
                    if (pedido.Product.IsInventoryTracked)
                    {
                        if (!datos.ReceivedQuantity.HasValue || datos.ReceivedQuantity.Value <= 0)
                        {
                            throw new Exception($"El producto '{pedido.Product.Name}' es inventariable. Debes ingresar obligatoriamente una cantidad recibida mayor a cero.");
                        }
                    }

                    // --- 2.1 ACTUALIZAR DATOS DEL PADRE ---
                    if (datos.NewSupplierPrice.HasValue)
                        pedido.Product.SupplierPrice = datos.NewSupplierPrice.Value;

                    if (datos.NewProfitMargin.HasValue)
                        pedido.Product.ProfitMargin = datos.NewProfitMargin.Value;

                    // --- 2.2 ACTUALIZAR PRECIOS DE VENTA (Hijos) ---
                    if (datos.PresentationPrices != null && datos.PresentationPrices.Any() && pedido.Product.Presentations != null)
                    {
                        foreach (var actualizacionHijo in datos.PresentationPrices)
                        {
                            var presentacion = pedido.Product.Presentations.FirstOrDefault(p => p.Id == actualizacionHijo.PresentationId);

                            if (presentacion != null && presentacion.IsActive)
                            {
                                presentacion.Price = actualizacionHijo.NewPrice;
                            }
                        }
                    }

                    // --- 2.3 SUMAR AL INVENTARIO ---
                    // Como ya validamos arriba, aquí sabemos con seguridad que si es inventariable, trae cantidad válida.
                    if (pedido.Product.IsInventoryTracked)
                    {
                        // Bloqueo de fracciones
                        if (!pedido.Product.AllowFractions && (datos.ReceivedQuantity.Value % 1 != 0))
                        {
                            throw new InvalidOperationException($"El producto '{pedido.Product.Name}' está configurado para unidades enteras. No puedes ingresar {datos.ReceivedQuantity.Value}.");
                        }

                        decimal stockAnterior = pedido.Product.CurrentStock;
                        pedido.Product.CurrentStock += datos.ReceivedQuantity.Value;

                        var movimiento = new InventoryMovement
                        {
                            ProductId = pedido.Product.Id,
                            UserId = userId,
                            MovementType = MovementType.Entrada,
                            Quantity = datos.ReceivedQuantity.Value,
                            PreviousStock = stockAnterior,
                            NewStock = pedido.Product.CurrentStock,
                            Notes = "Entrada de mercancía por Proveedor", // TEXTO AUTOMÁTICO
                            CreatedAt = DateTime.Now
                        };
                        _context.InventoryMovements.Add(movimiento);
                    }

                    // --- 2.4 CERRAR EL PEDIDO ---
                    pedido.Status = PendingOrderStatus.Completado;
                    pedido.UserId = userId;
                    pedido.UpdatedAt = DateTime.Now;

                    _context.PendingOrderHistories.Add(new PendingOrderHistory
                    {
                        PendingOrderId = pedido.Id,
                        Status = PendingOrderStatus.Completado,
                        UserId = userId,
                        CreatedAt = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return pedido;
                }

                // ==========================================
                // ESCENARIO C: NO LLEGÓ, REGRESAR A PENDIENTE PARA VOLVER A PEDIR
                // ==========================================
                if (datos.FinalStatus == PendingOrderStatus.Pendiente)
                {
                    pedido.Status = PendingOrderStatus.Pendiente;
                    pedido.UserId = userId;
                    pedido.UpdatedAt = DateTime.Now;

                    _context.PendingOrderHistories.Add(new PendingOrderHistory
                    {
                        PendingOrderId = pedido.Id,
                        Status = PendingOrderStatus.Pendiente,
                        UserId = userId,
                        CreatedAt = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return pedido;
                }

                throw new Exception("El estado final proporcionado no es válido para esta operación (Debe ser Completado, Cancelado o Pendiente).");
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException("Alguien más acaba de modificar el stock de este producto. Recarga la página y vuelve a intentarlo.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}