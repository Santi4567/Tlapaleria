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
        private readonly IStockReservationService _stockReservationService;

        public ReturnService(TlapaleriaContext context, IStockReservationService stockReservationService)
        {
            _context = context;
            _stockReservationService = stockReservationService;
        }

        // GET: Obtener todos los reembolsos usando paginacion
        public async Task<PagedResponse<SaleReturn>> GetReturnsAsync(string? search = null, int pageNumber = 1, int pageSize = 50)
        {
            // Consulta base optimizada para solo lectura
            var query = _context.Returns
                .AsNoTracking() // <-- OPTIMIZACIÓN DE MEMORIA
                .Include(r => r.User)
                .Include(r => r.Sale)
                .Include(r => r.Details)
                    .ThenInclude(d => d.SaleDetail)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r =>
                    r.ReturnFolio.Contains(search) ||
                    (r.Sale != null && r.Sale.Folio.Contains(search)) ||
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

                // Validamos que el ticket no sea de cortesía o con 100% de descuento (Total $0.00)
                if (ticketOriginal.TotalAmount <= 0 || ticketOriginal.DiscountPercent >= 100m)
                {
                    throw new InvalidOperationException("No se pueden procesar devoluciones sobre una venta con el 100% de descuento o con un importe pagado de $0.00.");
                }

                // Validamos que el ticket aún tenga artículos disponibles para devolver
                decimal totalPiezasCompradas = await _context.SaleDetails
                    .Where(sd => sd.SaleId == dto.SaleId)
                    .SumAsync(sd => sd.Quantity);

                decimal totalPiezasYaDevueltas = await _context.ReturnDetails
                    .Where(rd => _context.SaleDetails.Any(sd => sd.Id == rd.SaleDetailId && sd.SaleId == dto.SaleId))
                    .SumAsync(rd => rd.QuantityReturned);

                if (totalPiezasYaDevueltas >= totalPiezasCompradas)
                {
                    throw new InvalidOperationException("Todos los artículos de este ticket ya fueron devueltos previamente (Devolución Total).");
                }

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

                var productosActualizados = new Dictionary<int, decimal>();

                // 2. Procesa cada producto
                foreach (var item in dto.Details)
                {
                    var renglonOriginal = await _context.SaleDetails
                        .FirstOrDefaultAsync(sd => sd.Id == item.SaleDetailId && sd.SaleId == dto.SaleId);

                    if (renglonOriginal == null)
                        throw new KeyNotFoundException($"El renglón con ID {item.SaleDetailId} no pertenece al ticket {dto.SaleId}.");

                    if (renglonOriginal.Subtotal <= 0)
                        throw new InvalidOperationException($"El producto '{renglonOriginal.ProductName}' no tuvo costo monetario tras el descuento y no admite devoluciones ni reembolsos.");

                    decimal cantidadYaDevuelta = await _context.ReturnDetails
                        .Where(rd => rd.SaleDetailId == item.SaleDetailId)
                        .SumAsync(rd => rd.QuantityReturned);

                    decimal cantidadDisponibleParaDevolver = renglonOriginal.Quantity - cantidadYaDevuelta;

                    if (item.QuantityReturned > cantidadDisponibleParaDevolver)
                        throw new InvalidOperationException($"Intento de fraude o error detectado en '{renglonOriginal.ProductName}'. Quieres devolver {item.QuantityReturned}, pero solo quedan {cantidadDisponibleParaDevolver} unidades disponibles.");

                    // Actualización de Almacén y Kardex
                    var productoBase = await _context.Products.FindAsync(renglonOriginal.ProductId);
                    if (productoBase != null && !productoBase.AllowFractions && (item.QuantityReturned % 1 != 0))
                    {
                        throw new InvalidOperationException($"El producto '{renglonOriginal.ProductName}' no permite devolver cantidades con decimales ({item.QuantityReturned}).");
                    }

                    // El reembolso se calcula sobre lo REALMENTE pagado (Subtotal es neto, ya con descuento prorrateado).
                    // Si se devuelve lo último que queda del renglón, se reembolsa el remanente exacto para no perder centavos.
                    decimal dineroYaReembolsado = await _context.ReturnDetails
                        .Where(rd => rd.SaleDetailId == item.SaleDetailId)
                        .SumAsync(rd => rd.RefundAmount);

                    decimal dineroAReembolsar = (item.QuantityReturned == cantidadDisponibleParaDevolver)
                        ? renglonOriginal.Subtotal - dineroYaReembolsado
                        : Math.Round(item.QuantityReturned * renglonOriginal.Subtotal / renglonOriginal.Quantity, 2, MidpointRounding.AwayFromZero);
                    devolucion.TotalRefunded += dineroAReembolsar;

                    var detalleDevolucion = new ReturnDetail
                    {
                        SaleDetailId = renglonOriginal.Id,
                        QuantityReturned = item.QuantityReturned,
                        RefundAmount = dineroAReembolsar
                    };
                    devolucion.Details.Add(detalleDevolucion);

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
                            productosActualizados[productoBase.Id] = productoBase.CurrentStock;
                        }
                    }
                }

                // La venta original permanece activa (IsActive = true) para registrar limpiamente
                // el ingreso en la venta y el egreso en la devolución, manteniendo la contabilidad transparente.

                _context.Returns.Add(devolucion);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                foreach (var (productId, nuevoStock) in productosActualizados)
                {
                    await _stockReservationService.BroadcastStockUpdateAsync(productId, nuevoStock);
                }

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
                    .ThenInclude(d => d.SaleDetail)
                .Where(r => r.SaleId == saleId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return historialDevoluciones;
        }
    }
}
