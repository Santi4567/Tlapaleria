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
    }
}