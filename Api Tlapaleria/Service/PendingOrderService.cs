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
        public async Task<PendingOrder> CreatePendingOrderAsync(CreatePendingOrderDto datos, int userId) // Recibimos el userId aquí
        {
            //Validamos que el producto no exista dentro de la tabal de pedidos para evitar duplicados 
            var pedidoExistente = await _context.PendingOrders.FirstOrDefaultAsync(po => po.ProductId == datos.ProductId && po.Status == PendingOrderStatus.Pendiente);

            if (pedidoExistente != null)
            {
                throw new Exception("El producto ya está anotado en la libreta como Pendiente. Si necesita cambiar algo, actualícelo.");
            }

            var producto = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == datos.ProductId && p.IsActive);

            if (producto == null)
                throw new Exception("El producto seleccionado no existe o se encuentra inactivo.");

            if (datos.SupplierId.HasValue)
            {
                var proveedor = await _context.Suppliers
                    .FirstOrDefaultAsync(s => s.Id == datos.SupplierId.Value && s.IsActive);

                if (proveedor == null)
                    throw new Exception("El proveedor seleccionado no existe o se encuentra inactivo.");
            }

            // El usuario se valida usando el userId seguro que viene del Token
            var usuario = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (usuario == null)
                throw new Exception("El usuario autenticado no es válido o está inactivo.");

            var nuevoPedido = new PendingOrder
            {
                ProductId = datos.ProductId,
                SupplierId = datos.SupplierId,
                UserId = userId, // Asignamos el ID del token 
                QuantityText = datos.QuantityText,
                Notes = datos.Notes,
                Status = PendingOrderStatus.Pendiente,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.PendingOrders.Add(nuevoPedido);
            await _context.SaveChangesAsync();

            await _context.Entry(nuevoPedido).Reference(p => p.Product).LoadAsync();
            if (nuevoPedido.SupplierId.HasValue)
                await _context.Entry(nuevoPedido).Reference(p => p.Supplier).LoadAsync();

            return nuevoPedido;
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

            // 1. Filtro: Buscador de texto (Meta 5)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower().Trim();
                query = query.Where(po =>
                    po.Product.Name.ToLower().Contains(term) ||
                    po.Product.InternalCode.ToLower().Contains(term) ||
                    po.Product.Barcode == term
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

            // Opcional pero recomendado: No dejar editar pedidos que ya se completaron
            if (pedidoExistente.Status == PendingOrderStatus.Completado)
                throw new Exception("No puedes modificar un pedido que ya ha sido completado y recibido.");

            // 2. Validamos el Proveedor (si es que mandaron uno o lo cambiaron)
            if (datos.SupplierId.HasValue && datos.SupplierId != pedidoExistente.SupplierId)
            {
                var proveedor = await _context.Suppliers
                    .FirstOrDefaultAsync(s => s.Id == datos.SupplierId.Value && s.IsActive);

                if (proveedor == null)
                    throw new Exception("El proveedor seleccionado no existe o se encuentra inactivo.");
            }

            // 3. Validar que el usuario que está editando exista
            var usuario = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (usuario == null)
                throw new Exception("El usuario autenticado no es válido o está inactivo.");

            // 4. Aplicamos los cambios permitidos
            pedidoExistente.QuantityText = datos.QuantityText;
            pedidoExistente.Notes = datos.Notes;
            pedidoExistente.SupplierId = datos.SupplierId;

            // 5. Actualizamos los rastros de auditoría
            pedidoExistente.UserId = userId; // El último que le metió mano
            pedidoExistente.UpdatedAt = DateTime.Now; // Fecha del cambio

            // 6. Guardamos en base de datos
            await _context.SaveChangesAsync();

            // Recargamos el proveedor por si lo cambiaron, para que el JSON regrese con el nombre correcto
            if (pedidoExistente.SupplierId.HasValue)
                await _context.Entry(pedidoExistente).Reference(p => p.Supplier).LoadAsync();

            return pedidoExistente;
        }

        //Actualizar el estado del producto
        public async Task<PendingOrder> UpdatePendingOrderStatusAsync(int id, PendingOrderStatus status, int userId)
        {
            // 1. Validar que el estado enviado sea uno de los oficiales
            // (Comentario conservado, la validación se eliminó por el Enum)

            // 2. Buscar el pedido
            var pedidoExistente = await _context.PendingOrders
                .Include(po => po.Product)
                .Include(po => po.Supplier)
                .FirstOrDefaultAsync(po => po.Id == id);

            if (pedidoExistente == null)
                throw new Exception($"El pedido con ID {id} no existe.");

            // 3. Validar al usuario que está deslizando la tarjeta
            var usuario = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (usuario == null)
                throw new Exception("El usuario autenticado no es válido o está inactivo.");

            // 4. Aplicar el cambio de estado
            pedidoExistente.Status = status;

            // 5. Dejar rastro de quién lo hizo y a qué hora
            pedidoExistente.UserId = userId;
            pedidoExistente.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return pedidoExistente;
        }
    }
}