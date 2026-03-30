using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
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

        public async Task<PendingOrder> CreatePendingOrderAsync(CreatePendingOrderDto datos, int userId) // Recibimos el userId aquí
        {
            //Validamos que el producto no exista dentro de la tabal de pedidos para evitar duplicados 
            var pedidoExistente = await _context.PendingOrders.FirstOrDefaultAsync(po => po.ProductId == datos.ProductId && po.Status == "Pendiente");

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
                Status = "Pendiente",
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
        //Obtener todos los productos por proveedor y por estado 
        public async Task<PagedResponse<PendingOrder>> GetPendingOrdersBySupplierAsync(int supplierId, string status = "Pendiente", int pageNumber = 1, int pageSize = 50)
        {
            // 1. Validar que el estado sea válido (incluyendo nuestro comodín)
            var estadosValidos = new List<string> { "Pendiente", "Cancelado", "Completado", "Todos" };

            // Si el front manda minúsculas o espacios extra, lo limpiamos y capitalizamos la primera letra
            // (Opcional, pero ayuda a evitar errores de tipeo del frontend)
            if (!string.IsNullOrWhiteSpace(status))
            {
                status = char.ToUpper(status[0]) + status.Substring(1).ToLower();
            }

            if (!estadosValidos.Contains(status))
                throw new Exception($"El filtro de estado '{status}' no es válido. Usa uno de estos: {string.Join(", ", estadosValidos)}.");

            // 2. Armamos la consulta base
            var query = _context.PendingOrders
                .Include(po => po.Product)
                .Include(po => po.Supplier)
                .Include(po => po.User)
                .AsQueryable();

            // 3. Filtro obligatorio por Proveedor
            if (supplierId == 0)
            {
                query = query.Where(po => po.SupplierId == null);
            }
            else
            {
                query = query.Where(po => po.SupplierId == supplierId);
            }

            // --- 4. LA MAGIA HÍBRIDA (Filtro por Estado) ---
            if (status != "Todos")
            {
                // Si no mandaron el comodín, filtramos estrictamente por lo que pidieron
                query = query.Where(po => po.Status == status);
            }
            // Si mandaron "Todos", simplemente nos saltamos este Where y la BD trae todo el historial

            // 5. Paginación y ejecución
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var pedidos = await query
                .OrderByDescending(po => po.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResponse<PendingOrder>
            {
                Data = pedidos,
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

        //Buscador por nombre,codigo, codigo de barras con estado 
        public async Task<PagedResponse<PendingOrder>> SearchPendingOrdersAsync(string searchTerm, string status = "Todos", int pageNumber = 1, int pageSize = 50)
        {
            // Si no hay texto, regresamos una respuesta paginada vacía
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new PagedResponse<PendingOrder>
                {
                    Data = new List<PendingOrder>(),
                    TotalItems = 0,
                    TotalPages = 0,
                    CurrentPage = pageNumber
                };

            var term = searchTerm.ToLower().Trim();

            // 1. Validar y limpiar el estado
            var estadosValidos = new List<string> { "Pendiente", "Cancelado", "Completado", "Todos" };

            if (!string.IsNullOrWhiteSpace(status))
            {
                status = char.ToUpper(status[0]) + status.Substring(1).ToLower();
            }

            if (!estadosValidos.Contains(status))
                throw new Exception($"El filtro de estado '{status}' no es válido. Usa: {string.Join(", ", estadosValidos)}.");

            // 2. Armamos la consulta base con la búsqueda de texto
            var query = _context.PendingOrders
                .Include(po => po.Product)
                .Include(po => po.Supplier)
                .Include(po => po.User)
                .Where(po =>
                    po.Product.Name.ToLower().Contains(term) ||
                    po.Product.InternalCode.ToLower().Contains(term) ||
                    po.Product.Barcode == term
                )
                .AsQueryable();

            // 3. Aplicamos el filtro híbrido de estado
            if (status != "Todos")
            {
                query = query.Where(po => po.Status == status);
            }

            // --- 4. LA NUEVA MAGIA: Paginación en la búsqueda ---
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var resultados = await query
                .OrderByDescending(po => po.CreatedAt) // Los más recientes primero
                .Skip((pageNumber - 1) * pageSize)     // Nos saltamos las páginas anteriores
                .Take(pageSize)                        // Tomamos solo los de esta página
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
            if (pedidoExistente.Status == "Completado")
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
        public async Task<PendingOrder> UpdatePendingOrderStatusAsync(int id, string status, int userId)
        {
            // 1. Validar que el estado enviado sea uno de los oficiales
            var estadosValidos = new List<string> { "Pendiente", "Cancelado", "Completado" };

            if (!estadosValidos.Contains(status))
                throw new Exception($"El estado '{status}' no es válido. Usa uno de estos: {string.Join(", ", estadosValidos)}");

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