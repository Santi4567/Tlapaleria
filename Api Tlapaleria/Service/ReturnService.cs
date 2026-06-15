using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
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

        public async Task<PagedResponse<SaleReturn>> GetReturnsAsync(string? search = null, int pageNumber = 1, int pageSize = 50)
        {
            // Empezamos la consulta base uniendo las tablas necesarias (pero SIN los detalles para no saturar)
            var query = _context.Returns
                .Include(r => r.User)
                .Include(r => r.Sale)
                .AsQueryable();

            // Si hay algo en la barra de búsqueda, filtramos por Folio o por el Motivo
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r =>
                    r.ReturnFolio.Contains(search) ||
                    (r.Reason != null && r.Reason.Contains(search))
                );
            }

            // Matemáticas de paginación
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Traemos los datos ordenados desde la devolución más reciente
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


        //Crear una devolucion 
        public async Task<SaleReturn> CreateReturnAsync(CreateReturnDto dto, int userId)
        {
            // 1. INICIAMOS LA BÓVEDA (Transacción)
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 2. Validamos que el ticket original exista
                var ticketOriginal = await _context.Sales
                    .FirstOrDefaultAsync(s => s.Id == dto.SaleId);

                if (ticketOriginal == null || !ticketOriginal.IsActive)
                    throw new Exception("El ticket original no existe o ya se encuentra totalmente cancelado.");

                // Generamos el Folio Único de Devolución
                var random = new Random();
                string folioDevolucion = $"DEV-{DateTime.Now:yyMMddHHmmssfff}-{random.Next(1000, 10000)}";

                // Preparamos la cabecera de la devolución
                var devolucion = new SaleReturn
                {
                    ReturnFolio = folioDevolucion,
                    SaleId = dto.SaleId,
                    UserId = userId,
                    Reason = dto.Reason,
                    TotalRefunded = 0,
                    CreatedAt = DateTime.Now
                };

                // 3. EL BUCLE ANTI-FRAUDES (Procesa cada producto)
                foreach (var item in dto.Details)
                {
                    var renglonOriginal = await _context.SaleDetails
                        .FirstOrDefaultAsync(sd => sd.Id == item.SaleDetailId && sd.SaleId == dto.SaleId);

                    if (renglonOriginal == null)
                        throw new Exception($"El renglón con ID {item.SaleDetailId} no pertenece al ticket {dto.SaleId}.");

                    // ¿Cuánto ha devuelto de este renglón en el pasado?
                    decimal cantidadYaDevuelta = await _context.ReturnDetails
                        .Where(rd => rd.SaleDetailId == item.SaleDetailId)
                        .SumAsync(rd => rd.QuantityReturned);

                    // Matemáticas de disponibilidad
                    decimal cantidadDisponibleParaDevolver = renglonOriginal.Quantity - cantidadYaDevuelta;

                    if (item.QuantityReturned > cantidadDisponibleParaDevolver)
                        throw new Exception($"Intento de fraude o error detectado en '{renglonOriginal.ProductName}'. Quieres devolver {item.QuantityReturned}, pero solo quedan {cantidadDisponibleParaDevolver} unidades disponibles.");

                    // Dinero de este renglón
                    decimal dineroAReembolsar = item.QuantityReturned * renglonOriginal.UnitPrice;
                    devolucion.TotalRefunded += dineroAReembolsar;

                    // Armamos el detalle de la devolución
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
                        decimal cantidadBaseARegresar = item.QuantityReturned * renglonOriginal.StockFactorApplied;
                        decimal stockAnterior = productoBase.CurrentStock;

                        productoBase.CurrentStock += cantidadBaseARegresar;

                        var movimientoKardex = new InventoryMovement
                        {
                            ProductId = productoBase.Id,
                            UserId = userId,
                            MovementType = "Devolución",
                            Quantity = cantidadBaseARegresar,
                            PreviousStock = stockAnterior,
                            NewStock = productoBase.CurrentStock,
                            Notes = $"Devolución {folioDevolucion} ligada al ticket original {ticketOriginal.Folio}.",
                            CreatedAt = DateTime.Now
                        };
                        _context.InventoryMovements.Add(movimientoKardex);
                    }
                } // <--- AQUÍ TERMINA EL CICLO FOREACH


                // A. Sumamos cuántas piezas en total se compraron originalmente en ese ticket
                decimal totalPiezasCompradas = await _context.SaleDetails
                    .Where(sd => sd.SaleId == dto.SaleId)
                    .SumAsync(sd => sd.Quantity);

                // B. Sumamos cuántas piezas se devolvieron en el PASADO para este mismo ticket
                decimal totalPiezasDevueltasPasadas = await _context.ReturnDetails
                    .Where(rd => _context.SaleDetails.Any(sd => sd.Id == rd.SaleDetailId && sd.SaleId == dto.SaleId))
                    .SumAsync(rd => rd.QuantityReturned);

                // C. Sumamos las piezas que se están devolviendo AHIRITA en esta llamada
                decimal totalPiezasDevueltasHoy = devolucion.Details.Sum(d => d.QuantityReturned);

                // D. Computamos el gran total devuelto
                decimal totalPiezasDevueltas = totalPiezasDevueltasPasadas + totalPiezasDevueltasHoy;

                // E. Si ya se regresó todo, el ticket original pasa a IsActive = false (0)
                if (totalPiezasDevueltas >= totalPiezasCompradas)
                {
                    ticketOriginal.IsActive = false;
                    _context.Sales.Update(ticketOriginal);
                }

                // 5. GUARDAMOS TODO EL HISTORIAL (LEDGER) EN BASE DE DATOS
                _context.Returns.Add(devolucion);
                await _context.SaveChangesAsync();

                // Confirmamos la transacción
                await transaction.CommitAsync();

                // Cargamos relaciones para la respuesta JSON limpia
                await _context.Entry(devolucion).Reference(d => d.User).LoadAsync();
                await _context.Entry(devolucion).Reference(d => d.Sale).LoadAsync();

                return devolucion;
            }
            catch (Exception)
            {
                // Si algo falló arriba, no se guarda nada de nada
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}