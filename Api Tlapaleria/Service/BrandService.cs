using System.Text.RegularExpressions;
using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class BrandService : IBrandService
    {
        private readonly TlapaleriaContext _context;

        public BrandService(TlapaleriaContext context)
        {
            _context = context;
        }

        // "  truper   tools " -> "truper tools"
        private static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new Exception("El nombre de la marca es obligatorio.");

            name = Regex.Replace(name.Trim(), @"\s+", " ");

            if (name.Length > 100)
                throw new Exception("El nombre de la marca no puede pasar de 100 caracteres.");

            return name;
        }

        // IMPORTANTE: llamarlo ANTES de abrir una transacción en ProductService.
        // Si dos usuarios crean la misma marca a la vez, el índice UNIQUE rechaza al segundo
        // y aquí lo volvemos a buscar. Dentro de una transacción InnoDB (REPEATABLE READ)
        // esa segunda búsqueda no vería la fila recién confirmada por el otro usuario.
        public async Task<Brand> GetOrCreateAsync(string name)
        {
            name = Normalize(name);

            // La collation _ci hace que "TRUPER", "Truper" y "truper" sean la misma marca
            var existente = await _context.Brands.FirstOrDefaultAsync(b => b.Name == name);
            if (existente != null)
            {
                if (!existente.IsActive)
                {
                    existente.IsActive = true;
                    await _context.SaveChangesAsync();
                }
                return existente;
            }

            var nueva = new Brand { Name = name };
            _context.Brands.Add(nueva);

            try
            {
                await _context.SaveChangesAsync();
                return nueva;
            }
            catch (DbUpdateException)
            {
                // Otro usuario la creó justo ahora: descartamos la nuestra y buscamos la suya
                _context.Entry(nueva).State = EntityState.Detached;
                return await _context.Brands.FirstAsync(b => b.Name == name);
            }
        }

        public async Task<List<BrandDto>> SearchAsync(string q)
        {
            q = (q ?? "").Trim();

            // StartsWith -> LIKE 'q%' : aprovecha el índice UNIQUE de Name
            return await _context.Brands
                .AsNoTracking()
                .Where(b => b.IsActive && b.Name.StartsWith(q))
                .OrderBy(b => b.Name)
                .Take(10)
                .Select(b => new BrandDto(b.Id, b.Name))
                .ToListAsync();
        }

        public async Task<List<BrandDto>> GetAllAsync()
        {
            return await _context.Brands
                .AsNoTracking()
                .Where(b => b.IsActive)
                .OrderBy(b => b.Name)
                .Select(b => new BrandDto(b.Id, b.Name))
                .ToListAsync();
        }

        public async Task<BrandDto> RenameAsync(int id, string newName)
        {
            newName = Normalize(newName);

            var brand = await _context.Brands.FindAsync(id)
                ?? throw new Exception("La marca no existe.");

            var nombreAnterior = brand.Name;

            // Si ya existe OTRA marca con ese nombre, hay que fusionar, no renombrar
            bool yaExiste = await _context.Brands.AnyAsync(b => b.Name == newName && b.Id != id);
            if (yaExiste)
                throw new Exception($"Ya existe una marca llamada '{newName}'. Usa 'fusionar' para unirlas.");

            await using var tx = await _context.Database.BeginTransactionAsync();

            brand.Name = newName;
            await _context.SaveChangesAsync();

            // Un solo UPDATE en MySQL, sin cargar productos en memoria (requiere EF Core 7+)
            await _context.Products
                .Where(p => p.Brand == nombreAnterior)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Brand, newName));

            await tx.CommitAsync();

            return new BrandDto(brand.Id, brand.Name);
        }

        public async Task<int> MergeAsync(int sourceId, int targetId)
        {
            if (sourceId == targetId)
                throw new Exception("La marca origen y destino son la misma.");

            var source = await _context.Brands.FindAsync(sourceId)
                ?? throw new Exception("La marca origen no existe.");
            var target = await _context.Brands.FindAsync(targetId)
                ?? throw new Exception("La marca destino no existe.");

            // Todo o nada: si algo falla, no queda nada a medias
            await using var tx = await _context.Database.BeginTransactionAsync();

            var migrados = await _context.Products
                .Where(p => p.Brand == source.Name)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Brand, target.Name));

            await _context.Brands.Where(b => b.Id == sourceId).ExecuteDeleteAsync();

            await tx.CommitAsync();

            return migrados;
        }

        public async Task DeleteAsync(int id)
        {
            var brand = await _context.Brands.FindAsync(id)
                ?? throw new Exception("La marca no existe.");

            var enUso = await _context.Products.CountAsync(p => p.Brand == brand.Name);
            if (enUso > 0)
                throw new Exception(
                    $"No se puede eliminar: {enUso} producto(s) usan esta marca. Usa 'fusionar' para migrarlos primero.");

            _context.Brands.Remove(brand);
            await _context.SaveChangesAsync();
        }
    }
}
