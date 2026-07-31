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
        //ENPOINT MAESTRO 4 EN 1
        // ID del producto. Si es 'null', Entity Framework ignora este filtro y trae el catálogo completo.
        //int? productId = null,

        // Fecha de inicio. Si tiene valor, la consulta agregará un: WHERE CreatedAt >= 'startDate 00:00:00'
        //DateTime? startDate = null,

        // Fecha de fin. Si tiene valor, la consulta agregará un: WHERE CreatedAt <= 'endDate 23:59:59'
        //DateTime? endDate = null,

        // El tipo de movimiento. Si tiene valor (ej. 2), la consulta filtrará: WHERE MovementType = 2
        //MovementType? movementType = null,

        // Página solicitada. Se usa para calcular el salto de registros (.Skip)
        //int pageNumber = 1,

        // Límite de registros. Se usa para tomar solo ese bloque de datos (.Take)
        ///int pageSize = 50);
        public async Task<PagedResponse<InventoryMovement>> GetMovementsAsync(
            int? productId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            MovementType? movementType = null,
            int pageNumber = 1,
            int pageSize = 50)
        {
            // 1. Iniciamos la consulta optimizada
            var query = _context.InventoryMovements
                .AsNoTracking()
                .Include(m => m.Product)
                .Include(m => m.User)
                .AsQueryable();

            // 2. Filtro 1: Por Producto (Si lo mandan)
            if (productId.HasValue)
            {
                query = query.Where(m => m.ProductId == productId.Value);
            }

            // 3. Filtro 2: Por Tipo de Movimiento (Enum)
            if (movementType.HasValue)
            {
                query = query.Where(m => m.MovementType == movementType.Value);
            }

            // 4. Filtro 3: Por Rango de Fechas
            if (startDate.HasValue)
            {
                // Ignoramos la hora para que empiece a buscar desde las 00:00:00 de ese día
                query = query.Where(m => m.CreatedAt >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                // Extendemos la fecha de fin hasta las 23:59:59 para incluir todo ese día
                var finDelDia = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(m => m.CreatedAt <= finDelDia);
            }

            // 5. Matemáticas de paginación
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // 6. Ejecutamos ordenando siempre del más reciente al más antiguo
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