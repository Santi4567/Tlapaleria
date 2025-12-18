using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models; // Tu modelo User
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Api_Tlapaleria.Services
{
    public class AuthService
    {
        private readonly TlapaleriaContext _context;
        private readonly IConfiguration _config;

        public AuthService(TlapaleriaContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public async Task<string?> LoginAsync(LoginDto login)
        {
            // 1. Buscar usuario por Nombre de Usuario (O Correo si tuvieras la columna)
            // 1. CAMBIO: Agregamos .Include(u => u.Rol)
            // Esto le dice a EF: "Cuando traigas al usuario, ve a la tabla Roles y tráeme sus datos también"
            var user = await _context.Users
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Username == login.UsuarioOCorreo);

            // 2. Si no existe el usuario, retornamos null (Login fallido)
            if (user == null) return null;

            // --- Verificamos si está activo ---
            if (!user.IsActive)
            {
                // Opcional: Podrías lanzar una excepción específica o retornar null
                // throw new Exception("Tu cuenta ha sido desactivada.");
                return null;
            }

            // 3. Verificar contraseña usando BCrypt
            bool passwordValido = BCrypt.Net.BCrypt.Verify(login.Password, user.Passwd);
            if (!passwordValido) return null;

            // 4. Generar el Token JWT
            return GenerarToken(user);
        }

        private string GenerarToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
        
                // 2. CAMBIO: Accedemos al Nombre a través del objeto Rol
                new Claim(ClaimTypes.Role, user.Rol.Nombre)
            };

            var token = new JwtSecurityToken(
                _config["Jwt:Issuer"],
                _config["Jwt:Audience"],
                claims,
                expires: DateTime.Now.AddMinutes(double.Parse(_config["Jwt:ExpireMinutes"])),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}