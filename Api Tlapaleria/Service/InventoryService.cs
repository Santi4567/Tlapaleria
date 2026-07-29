using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Enums;
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly TlapaleriaContext _context;

        public InventoryService(TlapaleriaContext context)
        {
            _context = context;
        }

        public async Task<InventoryMovement> RegisterMovementAsync(CreateInventoryMovementDto datos, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Buscamos el producto principal
                var producto = await _context.Products.FindAsync(datos.ProductId);
                if (producto == null || !producto.IsActive)
                    throw new KeyNotFoundException("El producto no existe o está inactivo.");

                // --- NUEVA REGLA DE NEGOCIO: Validar Fracciones ---
                // Si el producto NO permite fracciones y la cantidad tiene decimales...
                if (!producto.AllowFractions && (datos.Quantity % 1 != 0))
                {
                    throw new InvalidOperationException($"El producto '{producto.Name}' está configurado para venderse solo por unidades enteras. No puedes ingresar cantidades fraccionadas como {datos.Quantity}.");
                }
                // --------------------------------------------------

                // 2. Tomamos la fotografía del stock actual
                decimal stockAnterior = producto.CurrentStock;
                decimal nuevoStock = stockAnterior;

                // 3. Calculamos usando el Enum fuertemente tipado
                if (datos.MovementType == MovementType.Entrada || datos.MovementType == MovementType.AjustePositivo)
                {
                    nuevoStock += datos.Quantity;
                }
                else if (datos.MovementType == MovementType.Merma || datos.MovementType == MovementType.AjusteNegativo)
                {
                    nuevoStock -= datos.Quantity;
                }

                if (nuevoStock < 0)
                    throw new InvalidOperationException($"Operación inválida. El stock actual es {stockAnterior} y no puedes restar {datos.Quantity}.");

                // 4. Actualizamos el padre
                producto.CurrentStock = nuevoStock;
                producto.UpdatedAt = DateTime.Now;

                // 5. Creamos el historial de movimiento
                var movimiento = new InventoryMovement
                {
                    ProductId = datos.ProductId,
                    UserId = userId,
                    MovementType = datos.MovementType,
                    Quantity = datos.Quantity,
                    PreviousStock = stockAnterior,
                    NewStock = nuevoStock,
                    Notes = datos.Notes,
                    CreatedAt = DateTime.Now
                };

                _context.InventoryMovements.Add(movimiento);

                // 6. Guardamos los cambios. Si hay conflicto de concurrencia, saltará al catch.
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Cargamos info extra para la respuesta
                await _context.Entry(movimiento).Reference(m => m.Product).LoadAsync();
                await _context.Entry(movimiento).Reference(m => m.User).LoadAsync();

                return movimiento;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException("El stock fue modificado por otro usuario en este instante. Por favor, recarga y vuelve a intentarlo para evitar errores de inventario.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<PagedResponse<InventoryMovement>> GetMovementsByProductIdAsync(int productId, int pageNumber = 1, int pageSize = 50)
        {
            var productoExiste = await _context.Products.AnyAsync(p => p.Id == productId);
            if (!productoExiste)
                throw new KeyNotFoundException($"El producto con ID {productId} no existe.");

            var query = _context.InventoryMovements
                .AsNoTracking() // Optimización de memoria
                .Include(m => m.Product)
                .Include(m => m.User)
                .Where(m => m.ProductId == productId)
                .AsQueryable();

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var movimientos = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResponse<InventoryMovement>
            {
                Data = movimientos,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };
        }
    }
}