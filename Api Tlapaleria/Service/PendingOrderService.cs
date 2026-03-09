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
    }
}