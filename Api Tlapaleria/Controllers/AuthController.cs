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
            // Ahora LoginAsync devuelve una tupla con los dos tokens
            var tokens = await _authService.LoginAsync(loginDto);

            if (tokens == null)
            {
                return Unauthorized(ApiResponse<object>.Error("Usuario o contraseña incorrectos (o cuenta inactiva)"));
            }

            // Configuramos las cookies
            SetTokenCookies(tokens.Value.AccessToken, tokens.Value.RefreshToken);

            var datosRespuesta = new
            {
                usuario = loginDto.UsuarioOCorreo,
                token = tokens.Value.AccessToken // Mantenemos esto por si una app móvil lo requiere
            };

            return Ok(ApiResponse<object>.Exito(datosRespuesta, "Login exitoso"));
        }

        // NUEVO ENDPOINT: Para renovar la sesión sin pedir credenciales
        [HttpPost("refresh")]
        public async Task<ActionResult<ApiResponse<object>>> Refresh()
        {
            // Extraemos el refresh token de la cookie
            var refreshToken = Request.Cookies["refresh_token"];

            if (string.IsNullOrEmpty(refreshToken))
            {
                return Unauthorized(ApiResponse<object>.Error("No hay sesión activa para renovar"));
            }

            // Llamamos al servicio para validar y generar nuevos tokens
            var newTokens = await _authService.RefreshSessionAsync(refreshToken);

            if (newTokens == null)
            {
                // Si el refresh token expiró o es inválido, limpiamos las cookies
                Response.Cookies.Delete("access_token");
                Response.Cookies.Delete("refresh_token");
                return Unauthorized(ApiResponse<object>.Error("La sesión ha expirado por completo. Vuelve a iniciar sesión."));
            }

            // Actualizamos las cookies con los nuevos tokens
            SetTokenCookies(newTokens.Value.AccessToken, newTokens.Value.RefreshToken);

            return Ok(ApiResponse<object>.Exito(new { token = newTokens.Value.AccessToken }, "Sesión renovada con éxito"));
        }

        // Método auxiliar para no repetir código al configurar cookies
        private void SetTokenCookies(string accessToken, string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Cámbialo a true en Producción (HTTPS)
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.Now.AddMinutes(60) // Tiempo de vida de la cookie del JWT
            };

            var refreshCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Cámbialo a true en Producción (HTTPS)
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.Now.AddDays(7) // Tiempo de vida del Refresh Token (debe coincidir con la BD)
            };

            Response.Cookies.Append("access_token", accessToken, cookieOptions);
            Response.Cookies.Append("refresh_token", refreshToken, refreshCookieOptions);
        }
    }
}