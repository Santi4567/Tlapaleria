using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api_Tlapaleria.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<object>>> Login([FromBody] LoginDto loginDto)
        {
            var tokens = await _authService.LoginAsync(loginDto);

            if (tokens == null)
            {
                return Unauthorized(ApiResponse<object>.Error("Usuario o contraseña incorrectos (o cuenta inactiva)"));
            }

            // --- MAGIA AQUÍ: Guardamos los tokens en las cookies HttpOnly ---
            SetTokenCookies(tokens.Value.AccessToken, tokens.Value.RefreshToken);

            // Exponemos AMBOS tokens en el JSON (útil para clientes que no usan cookies, como Postman o Tauri puro)
            var datosRespuesta = new
            {
                usuario = loginDto.UsuarioOCorreo,
                token = tokens.Value.AccessToken,
                refreshToken = tokens.Value.RefreshToken
            };

            return Ok(ApiResponse<object>.Exito(datosRespuesta, "Login exitoso"));
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<ApiResponse<object>>> Refresh([FromBody] RefreshRequestDto request)
        {
            // Intentamos leer el token del Body, si no viene, lo buscamos en la cookie HttpOnly
            string refreshToken = request?.RefreshToken ?? Request.Cookies["refresh_token"];

            if (string.IsNullOrEmpty(refreshToken))
            {
                return Unauthorized(ApiResponse<object>.Error("No hay sesión activa para renovar"));
            }

            var newTokens = await _authService.RefreshSessionAsync(refreshToken);

            if (newTokens == null)
            {
                return Unauthorized(ApiResponse<object>.Error("La sesión ha expirado por completo. Vuelve a iniciar sesión."));
            }

            // --- MAGIA AQUÍ: Renovamos las cookies con los nuevos tokens ---
            SetTokenCookies(newTokens.Value.AccessToken, newTokens.Value.RefreshToken);

            // Devolvemos los tokens renovados
            var datosRespuesta = new
            {
                token = newTokens.Value.AccessToken,
                refreshToken = newTokens.Value.RefreshToken
            };

            return Ok(ApiResponse<object>.Exito(datosRespuesta, "Sesión renovada con éxito"));
        }

        [HttpPost("logout")]
        public async Task<ActionResult<ApiResponse<object>>> Logout([FromBody] RefreshRequestDto request)
        {
            // Intentamos leer el token del Body o de la cookie
            string refreshToken = request?.RefreshToken ?? Request.Cookies["refresh_token"];

            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _authService.LogoutAsync(refreshToken);
            }

            // --- MAGIA AQUÍ: Borramos las cookies para cerrar la sesión en el navegador/Swagger ---
            Response.Cookies.Delete("access_token");
            Response.Cookies.Delete("refresh_token");

            return Ok(ApiResponse<object>.Exito(null, "Sesión cerrada correctamente"));
        }

        // Método auxiliar para configurar cookies
        private void SetTokenCookies(string accessToken, string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Cámbialo a false si haces pruebas locales en HTTP, sino Swagger no las guardará
                SameSite = SameSiteMode.None,
                Expires = DateTime.Now.AddMinutes(60) // Tiempo de vida de la cookie del JWT
            };

            var refreshCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Cámbialo a false si haces pruebas locales en HTTP
                SameSite = SameSiteMode.None,
                Expires = DateTime.Now.AddDays(7) // Tiempo de vida del Refresh Token (debe coincidir con la BD)
            };

            Response.Cookies.Append("access_token", accessToken, cookieOptions);
            Response.Cookies.Append("refresh_token", refreshToken, refreshCookieOptions);
        }
    }
}