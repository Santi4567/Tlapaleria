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

            // Exponemos AMBOS tokens en el JSON
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
            if (string.IsNullOrEmpty(request.RefreshToken))
            {
                return Unauthorized(ApiResponse<object>.Error("No hay sesión activa para renovar"));
            }

            var newTokens = await _authService.RefreshSessionAsync(request.RefreshToken);

            if (newTokens == null)
            {
                return Unauthorized(ApiResponse<object>.Error("La sesión ha expirado por completo. Vuelve a iniciar sesión."));
            }

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
            if (string.IsNullOrEmpty(request.RefreshToken))
            {
                return BadRequest(ApiResponse<object>.Error("Refresh token ausente"));
            }

            await _authService.LogoutAsync(request.RefreshToken);

            return Ok(ApiResponse<object>.Exito(null, "Sesión cerrada correctamente"));
        }

        // Método auxiliar para no repetir código al configurar cookies
        // No se usa por el momento ya que nuestra propia app admisnitra las cookies no el navegador 
        private void SetTokenCookies(string accessToken, string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Cámbialo a true en Producción (HTTPS)
                SameSite = SameSiteMode.None,
                Expires = DateTime.Now.AddMinutes(60) // Tiempo de vida de la cookie del JWT
            };

            var refreshCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Cámbialo a true en Producción (HTTPS)
                SameSite = SameSiteMode.None,
                Expires = DateTime.Now.AddDays(7) // Tiempo de vida del Refresh Token (debe coincidir con la BD)
            };

            Response.Cookies.Append("access_token", accessToken, cookieOptions);
            Response.Cookies.Append("refresh_token", refreshToken, refreshCookieOptions);
        }
    }
}