using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Security.Cryptography;

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

        public async Task<(string AccessToken, string RefreshToken)?> LoginAsync(LoginDto login)
        {
            var user = await _context.Users
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Username == login.UsuarioOCorreo);

            if (user == null || !user.IsActive) return null;

            bool passwordValido = BCrypt.Net.BCrypt.Verify(login.Password, user.Passwd);
            if (!passwordValido) return null;

            var jwtToken = GenerarToken(user);
            var refreshTokenPlano = GenerateRefreshToken();
            var refreshTokenHash = HashRefreshToken(refreshTokenPlano); // Hasheamos para la BD

            // 1. LIMPIEZA: Eliminar sesiones expiradas de este usuario
            var sesionesExpiradas = await _context.UserSessions
                .Where(s => s.UserId == user.Id && s.ExpiryTime <= DateTime.UtcNow)
                .ToListAsync();

            if (sesionesExpiradas.Any())
            {
                _context.UserSessions.RemoveRange(sesionesExpiradas);
            }

            // 2. INSERCIÓN: Guardar la nueva sesión con el token hasheado
            var nuevaSesion = new UserSession
            {
                UserId = user.Id,
                RefreshToken = refreshTokenHash,
                ExpiryTime = DateTime.UtcNow.AddDays(7)
            };

            _context.UserSessions.Add(nuevaSesion);
            await _context.SaveChangesAsync();

            // Devolvemos el token plano para que el controlador lo envíe al usuario
            return (jwtToken, refreshTokenPlano);
        }

        public async Task<(string AccessToken, string RefreshToken)?> RefreshSessionAsync(string oldRefreshTokenPlano)
        {
            var oldTokenHash = HashRefreshToken(oldRefreshTokenPlano);

            // Buscamos usando el HASH
            var session = await _context.UserSessions
                .Include(s => s.User)
                .ThenInclude(u => u.Rol)
                .FirstOrDefaultAsync(s => s.RefreshToken == oldTokenHash);

            if (session == null || !session.User.IsActive || session.ExpiryTime <= DateTime.UtcNow)
            {
                return null;
            }

            // Generamos los nuevos
            var newJwtToken = GenerarToken(session.User);
            var newRefreshTokenPlano = GenerateRefreshToken();
            var newRefreshTokenHash = HashRefreshToken(newRefreshTokenPlano);

            // Actualizamos esta sesión específica
            session.RefreshToken = newRefreshTokenHash;
            session.ExpiryTime = DateTime.UtcNow.AddDays(7);

            await _context.SaveChangesAsync();

            return (newJwtToken, newRefreshTokenPlano);
        }

        // NUEVO: Método para cerrar sesión destruyendo el token
        public async Task<bool> LogoutAsync(string refreshTokenPlano)
        {
            var tokenHash = HashRefreshToken(refreshTokenPlano);

            var session = await _context.UserSessions
                .FirstOrDefaultAsync(s => s.RefreshToken == tokenHash);

            if (session != null)
            {
                _context.UserSessions.Remove(session);
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }

        // Método privado para aplicar SHA-256
        private string HashRefreshToken(string token)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(token);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private string GenerarToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Rol.Nombre)
            };

            var token = new JwtSecurityToken(
                _config["Jwt:Issuer"],
                _config["Jwt:Audience"],
                claims,
                // Tiempo de vida corto para el JWT (ej. 15 o 30 minutos)
                expires: DateTime.Now.AddMinutes(double.Parse(_config["Jwt:ExpireMinutes"])),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }

}