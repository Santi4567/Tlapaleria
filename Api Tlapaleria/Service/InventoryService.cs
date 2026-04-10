using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
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
            // INICIAMOS LA TRANSACCIÓN
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Buscamos el producto principal
                var producto = await _context.Products.FindAsync(datos.ProductId);
                if (producto == null || !producto.IsActive)
                    throw new Exception("El producto no existe o está inactivo.");

                // 2. Tomamos la "Fotografía" del stock actual
                decimal stockAnterior = producto.CurrentStock;
                decimal nuevoStock = stockAnterior;

                // 3. Calculamos la matemática dependiendo del tipo de movimiento
                if (datos.MovementType == "Entrada" || datos.MovementType == "Ajuste Positivo")
                {
                    nuevoStock += datos.Quantity;
                }
                else if (datos.MovementType == "Merma" || datos.MovementType == "Ajuste Negativo")
                {
                    nuevoStock -= datos.Quantity;
                }

                // Validación de negocio: No podemos tener stock negativo en una tlapalería
                if (nuevoStock < 0)
                    throw new Exception($"Operación inválida. El stock actual es {stockAnterior} y no puedes restar {datos.Quantity}.");

                // 4. ACTUALIZAMOS LA TABLA PADRE (PRODUCTS)
                producto.CurrentStock = nuevoStock;
                producto.UpdatedAt = DateTime.Now;

                // 5. CREAMOS EL HISTORIAL (KARDEX)
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

                // 6. Guardamos los cambios en ambas tablas
                await _context.SaveChangesAsync();

                // SI TODO SALIÓ BIEN, CONFIRMAMOS LA TRANSACCIÓN
                await transaction.CommitAsync();

                // Cargamos info extra para la respuesta JSON
                await _context.Entry(movimiento).Reference(m => m.Product).LoadAsync();
                await _context.Entry(movimiento).Reference(m => m.User).LoadAsync();

                return movimiento;
            }
            catch (Exception)
            {
                // SI ALGO FALLÓ (ej. error de BD o stock negativo), DESHACEMOS TODO
                await transaction.RollbackAsync();
                throw; // Lanzamos el error hacia el controlador
            }
        }
        public async Task<PagedResponse<InventoryMovement>> GetMovementsByProductIdAsync(int productId, int pageNumber = 1, int pageSize = 50)
        {
            // 1. Verificamos que el producto realmente exista antes de buscar su historial
            var productoExiste = await _context.Products.AnyAsync(p => p.Id == productId);
            if (!productoExiste)
                throw new Exception($"El producto con ID {productId} no existe.");

            // 2. Armamos la consulta a la tabla de Kardex
            var query = _context.InventoryMovements
                .Include(m => m.Product) // Para tener el nombre, código y unidad de medida
                .Include(m => m.User)    // Para saber quién hizo cada movimiento
                .Where(m => m.ProductId == productId)
                .AsQueryable();

            // 3. Contamos el total para la paginación
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // 4. Traemos solo la página que pidió el frontend, ordenado por fecha (el más nuevo primero)
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