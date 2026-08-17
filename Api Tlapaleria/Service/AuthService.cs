using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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

        // Modificamos el retorno para devolver ambos tokens
        public async Task<(string AccessToken, string RefreshToken)?> LoginAsync(LoginDto login)
        {
            var user = await _context.Users
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Username == login.UsuarioOCorreo);

            if (user == null || !user.IsActive) return null;

            bool passwordValido = BCrypt.Net.BCrypt.Verify(login.Password, user.Passwd);
            if (!passwordValido) return null;

            // Generamos ambos tokens
            var jwtToken = GenerarToken(user);
            var refreshToken = GenerateRefreshToken();

            // Los guardamos en la base de datos usando los nuevos campos de tu modelo User
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7); // Expira en 7 días

            await _context.SaveChangesAsync();

            return (jwtToken, refreshToken);
        }

        // Para validar y generar nuevos tokens
        public async Task<(string AccessToken, string RefreshToken)?> RefreshSessionAsync(string oldRefreshToken)
        {
            // Buscamos al usuario que tenga este refresh token exacto
            var user = await _context.Users
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.RefreshToken == oldRefreshToken);

            // Validamos que exista, esté activo y el token no haya expirado
            if (user == null || !user.IsActive || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return null;
            }

            // Si todo está bien, generamos un nuevo par de tokens
            var newJwtToken = GenerarToken(user);
            var newRefreshToken = GenerateRefreshToken();

            // Actualizamos la base de datos
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return (newJwtToken, newRefreshToken);
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