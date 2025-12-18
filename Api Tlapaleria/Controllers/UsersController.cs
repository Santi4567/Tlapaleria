using Api_Tlapaleria.Attributes; // Para usar [RequierePermiso]
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Api_Tlapaleria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Tlapaleria.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // 1. Nadie entra aquí sin Token
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost("create")]
        [RequierePermiso("add.users")] // 2. Solo pasa si en la BD su rol tiene este permiso
        public async Task<ActionResult<ApiResponse<User>>> CreateUser([FromBody] RegisterUserDto datos)
        {
            try
            {
                var usuarioCreado = await _userService.RegisterAsync(datos);

                return Ok(ApiResponse<User>.Exito(usuarioCreado, "Usuario registrado correctamente"));
            }
            catch (Exception ex)
            {
                // Si el rol no existe o el usuario ya existe, cae aquí
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
        [HttpGet("profile")]
        // No lleva [RequierePermiso] porque todos los usuarios logueados deberían poder ver su propio perfil
        public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetMyProfile()
        {
            try
            {
                // 1. LEER EL ID DEL TOKEN (Seguridad pura)
                // El ClaimTypes.NameIdentifier lo guardamos como ID numérico en el AuthService
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

                if (idClaim == null)
                    return Unauthorized(ApiResponse<object>.Error("Token inválido"));

                int userId = int.Parse(idClaim.Value);

                // 2. LLAMAR AL SERVICIO
                var perfil = await _userService.GetUserProfileAsync(userId);

                return Ok(ApiResponse<UserProfileDto>.Exito(perfil));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
    }
}