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
            var token = await _authService.LoginAsync(loginDto);

            if (token == null)
            {
                // Retornamos formato estándar de error
                return Unauthorized(ApiResponse<object>.Error("Usuario o contraseña incorrectos (o cuenta inactiva)"));
            }

            // Configurar Cookie... (Igual que antes)
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.Now.AddMinutes(60)
            };
            Response.Cookies.Append("access_token", token, cookieOptions);

            // Respuesta con datos (token para móvil + info usuario)
            var datosRespuesta = new
            {
                usuario = loginDto.UsuarioOCorreo,
                token = token
            };

            // Retornamos formato estándar de éxito
            return Ok(ApiResponse<object>.Exito(datosRespuesta, "Login exitoso"));
        }
    }
}