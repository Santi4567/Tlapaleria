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
            var pedidoExistente = await _context.PendingOrders
                .FirstOrDefaultAsync(po => po.ProductId == datos.ProductId && po.Status != "Completado");

            if (pedidoExistente != null)
                throw new Exception("El producto ya está agregado, si necesita cambiar algo actualícelo.");

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
        //Obtener todos los productos por proveedor 
        public async Task<PagedResponse<PendingOrder>> GetPendingOrdersBySupplierAsync(int supplierId, int pageNumber = 1, int pageSize = 50)
        {
            // 1. Armamos la consulta base incluyendo las relaciones
            var query = _context.PendingOrders
                .Include(po => po.Product)
                .Include(po => po.Supplier)
                .Include(po => po.User) // Opcional: Para saber qué empleado lo anotó
                .AsQueryable();

            // 2. Filtro obligatorio por Proveedor
            if (supplierId == 0)
            {
                // Si mandan 0, traemos los que todavía NO tienen proveedor asignado
                query = query.Where(po => po.SupplierId == null);
            }
            else
            {
                // Si mandan un ID mayor a 0, traemos los de ese proveedor específico
                query = query.Where(po => po.SupplierId == supplierId);
            }

            // Opcional pero recomendado: No mostrar los que ya están completados en esta vista
            // para mantener la libreta limpia. Si quieres ver el historial completo, puedes quitar esta línea.
            query = query.Where(po => po.Status != "Completado");

            // 3. Contamos el total para la paginación
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // 4. Traemos la página solicitada, ordenando los más recientes primero
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

        //Buscador por nombre,codigo, codigo de barras 
        public async Task<List<PendingOrder>> SearchPendingOrdersAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<PendingOrder>();

            var term = searchTerm.ToLower().Trim();

            var resultados = await _context.PendingOrders
                .Include(po => po.Product)
                .Include(po => po.Supplier)
                .Include(po => po.User)
                .Where(po =>
                    po.Product.Name.ToLower().Contains(term) || // Busca en el nombre del producto
                    po.Product.InternalCode.ToLower().Contains(term) || // Busca en el código interno
                    po.Product.Barcode == term // Busca si escanean el código
                )
                .OrderByDescending(po => po.CreatedAt) // Los más recientes primero
                .ToListAsync();

            return resultados;
        }
    }
}