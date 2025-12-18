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
    }
}