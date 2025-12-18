using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class UserService : IUserService
    {
        private readonly TlapaleriaContext _context;

        public UserService(TlapaleriaContext context)
        {
            _context = context;
        }

        public async Task<User> RegisterAsync(RegisterUserDto datos)
        {
            // 1. Validar que el usuario no exista
            var existe = await _context.Users.AnyAsync(u => u.Username == datos.Username);
            if (existe)
            {
                throw new Exception($"El usuario '{datos.Username}' ya existe.");
            }

            // 2. BUSCAR EL ID DEL ROL (La parte clave de la relación)
            var rolEncontrado = await _context.Roles
                .FirstOrDefaultAsync(r => r.Nombre == datos.RolNombre);

            if (rolEncontrado == null)
            {
                throw new Exception($"El rol '{datos.RolNombre}' no es válido.");
            }

            // 3. Encriptar contraseña
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(datos.Password);

            // 4. Crear el objeto User con la relación
            var nuevoUsuario = new User
            {
                Username = datos.Username,
                Passwd = passwordHash,
                Name = datos.Name,
                RolId = rolEncontrado.Id, // Asignamos el ID que encontramos en la BD
                IsActive = false 
            };

            // 5. Guardar
            _context.Users.Add(nuevoUsuario);
            await _context.SaveChangesAsync();

            // 6. Cargar la relación para devolver el objeto bonito (con nombre de rol)
            // Esto hace que la respuesta lleve "rol": { "id": 2, "nombre": "Vendedor" }
            await _context.Entry(nuevoUsuario).Reference(u => u.Rol).LoadAsync();

            return nuevoUsuario;
        }

        public async Task<UserProfileDto> GetUserProfileAsync(int userId)
        {
            // 1. Buscamos al usuario y TRAEMOS TODA LA FAMILIA (Rol y Permisos)
            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.Rol)                // Trae el Rol
                    .ThenInclude(r => r.Permisos)   // Trae los Permisos de ese Rol
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) throw new Exception("Usuario no encontrado");

            // 2. LOGICA DE AGRUPAMIENTO (El truco)
            // Suponemos que tus permisos son "verbo.entidad" (ej: "add.users")
            // Usamos la "entidad" (users) como nombre del grupo.

            var permisosAgrupados = user.Rol.Permisos
                .GroupBy(p => {
                    // Truco: Dividimos "add.users" por el punto y tomamos la última parte ("users")
                    // Si el permiso no tiene puntos, se usa el nombre completo.
                    var partes = p.NombreSistema.Split('.');
                    return partes.Length > 1 ? partes.Last() : "General";
                })
                .ToDictionary(
                    grupo => grupo.Key.ToUpper(), // La Clave: "USERS", "PRODUCTS"
                    grupo => grupo.Select(p => p.NombreSistema).ToList() // La Lista: ["add.users", "edit.users"]
                );

            // 3. Mapeamos al DTO
            return new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                Name = user.Name,
                Rol = user.Rol.Nombre,
                Permisos = permisosAgrupados
            };
        }
    }
}