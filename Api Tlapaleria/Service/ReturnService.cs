using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Enums; // <-- Importación del Enum
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class ReturnService : IReturnService
    {
        private readonly TlapaleriaContext _context;

        public ReturnService(TlapaleriaContext context)
        {
            _context = context;
        }

        // GET: Obtener todos los reembolsos usando paginacion
        public async Task<PagedResponse<SaleReturn>> GetReturnsAsync(string? search = null, int pageNumber = 1, int pageSize = 50)
        {
            // Consulta base optimizada para solo lectura
            var query = _context.Returns
                .AsNoTracking() // <-- OPTIMIZACIÓN DE MEMORIA
                .Include(r => r.User)
                .Include(r => r.Sale)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r =>
                    r.ReturnFolio.Contains(search) ||
                    (r.Reason != null && r.Reason.Contains(search))
                );
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var devoluciones = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResponse<SaleReturn>
            {
                Data = devoluciones,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };
        }

        // Crear una devolucion 
        public async Task<SaleReturn> CreateReturnAsync(CreateReturnDto dto, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Validamos que el ticket original exista y esté activo
                var ticketOriginal = await _context.Sales
                    .FirstOrDefaultAsync(s => s.Id == dto.SaleId);

                if (ticketOriginal == null || !ticketOriginal.IsActive)
                    throw new KeyNotFoundException("El ticket original no existe o ya se encuentra totalmente cancelado.");

                var random = new Random();
                string folioDevolucion = $"DEV-{DateTime.Now:yyMMddHHmmssfff}-{random.Next(1000, 10000)}";

                var devolucion = new SaleReturn
                {
                    ReturnFolio = folioDevolucion,
                    SaleId = dto.SaleId,
                    UserId = userId,
                    Reason = dto.Reason,
                    TotalRefunded = 0,
                    CreatedAt = DateTime.Now
                };

                // 2. Procesa cada producto
                foreach (var item in dto.Details)
                {
                    var renglonOriginal = await _context.SaleDetails
                        .FirstOrDefaultAsync(sd => sd.Id == item.SaleDetailId && sd.SaleId == dto.SaleId);

                    if (renglonOriginal == null)
                        throw new KeyNotFoundException($"El renglón con ID {item.SaleDetailId} no pertenece al ticket {dto.SaleId}.");

                    decimal cantidadYaDevuelta = await _context.ReturnDetails
                        .Where(rd => rd.SaleDetailId == item.SaleDetailId)
                        .SumAsync(rd => rd.QuantityReturned);

                    decimal cantidadDisponibleParaDevolver = renglonOriginal.Quantity - cantidadYaDevuelta;

                    if (item.QuantityReturned > cantidadDisponibleParaDevolver)
                        throw new InvalidOperationException($"Intento de fraude o error detectado en '{renglonOriginal.ProductName}'. Quieres devolver {item.QuantityReturned}, pero solo quedan {cantidadDisponibleParaDevolver} unidades disponibles.");

                    decimal dineroAReembolsar = item.QuantityReturned * renglonOriginal.UnitPrice;
                    devolucion.TotalRefunded += dineroAReembolsar;

                    var detalleDevolucion = new ReturnDetail
                    {
                        SaleDetailId = renglonOriginal.Id,
                        QuantityReturned = item.QuantityReturned,
                        RefundAmount = dineroAReembolsar
                    };
                    devolucion.Details.Add(detalleDevolucion);

                    // Actualización de Almacén y Kardex
                    var productoBase = await _context.Products.FindAsync(renglonOriginal.ProductId);
                    if (productoBase != null)
                    {
                        // LÓGICA HÍBRIDA: Solo regresamos stock si el producto lleva rastreo
                        if (productoBase.IsInventoryTracked)
                        {
                            decimal cantidadBaseARegresar = item.QuantityReturned * renglonOriginal.StockFactorApplied;
                            decimal stockAnterior = productoBase.CurrentStock;

                            productoBase.CurrentStock += cantidadBaseARegresar;

                            var movimientoKardex = new InventoryMovement
                            {
                                ProductId = productoBase.Id,
                                UserId = userId,
                                MovementType = MovementType.Devolucion, // <-- USO DEL NUEVO ENUM
                                Quantity = cantidadBaseARegresar,
                                PreviousStock = stockAnterior,
                                NewStock = productoBase.CurrentStock,
                                Notes = $"Devolución {folioDevolucion} ligada al ticket original {ticketOriginal.Folio}.",
                                CreatedAt = DateTime.Now
                            };
                            _context.InventoryMovements.Add(movimientoKardex);
                        }
                    }
                }

                // Lógica de desactivación de ticket completo
                decimal totalPiezasCompradas = await _context.SaleDetails
                    .Where(sd => sd.SaleId == dto.SaleId)
                    .SumAsync(sd => sd.Quantity);

                decimal totalPiezasDevueltasPasadas = await _context.ReturnDetails
                    .Where(rd => _context.SaleDetails.Any(sd => sd.Id == rd.SaleDetailId && sd.SaleId == dto.SaleId))
                    .SumAsync(rd => rd.QuantityReturned);

                decimal totalPiezasDevueltasHoy = devolucion.Details.Sum(d => d.QuantityReturned);
                decimal totalPiezasDevueltas = totalPiezasDevueltasPasadas + totalPiezasDevueltasHoy;

                if (totalPiezasDevueltas >= totalPiezasCompradas)
                {
                    ticketOriginal.IsActive = false;
                    _context.Sales.Update(ticketOriginal);
                }

                _context.Returns.Add(devolucion);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _context.Entry(devolucion).Reference(d => d.User).LoadAsync();
                await _context.Entry(devolucion).Reference(d => d.Sale).LoadAsync();

                return devolucion;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Obtener todo el historial de devoluciones ligadas a una misma venta
        public async Task<List<SaleReturn>> GetReturnsBySaleIdAsync(int saleId)
        {
            var historialDevoluciones = await _context.Returns
                .AsNoTracking() // <-- OPTIMIZACIÓN DE MEMORIA
                .Include(r => r.User)
                .Include(r => r.Details)
                .Where(r => r.SaleId == saleId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return historialDevoluciones;
        }
    }
}