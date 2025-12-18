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
                IsActive = true
            };

            // 5. Guardar
            _context.Users.Add(nuevoUsuario);
            await _context.SaveChangesAsync();

            // 6. Cargar la relación para devolver el objeto bonito (con nombre de rol)
            // Esto hace que la respuesta lleve "rol": { "id": 2, "nombre": "Vendedor" }
            await _context.Entry(nuevoUsuario).Reference(u => u.Rol).LoadAsync();

            return nuevoUsuario;
        }
    }
}