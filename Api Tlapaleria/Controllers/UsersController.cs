using Api_Tlapaleria.Attributes;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Api_Tlapaleria.Services;
using Api_Tlapaleria.Extensions; // Agregado para GetUserId()
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Tlapaleria.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost("create")]
        [RequierePermiso("add.users")]
        public async Task<ActionResult<ApiResponse<User>>> CreateUser([FromBody] RegisterUserDto datos)
        {
            int requestorId = User.GetUserId();
            var usuarioCreado = await _userService.RegisterAsync(datos, requestorId);
            return Ok(ApiResponse<User>.Exito(usuarioCreado, "Usuario registrado correctamente"));
        }

        [HttpGet("profile")]
        public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetMyProfile()
        {
            int userId = User.GetUserId();
            var perfil = await _userService.GetUserProfileAsync(userId);
            return Ok(ApiResponse<UserProfileDto>.Exito(perfil));
        }

        [HttpPut("update/{id}")]
        [RequierePermiso("edit.users")]
        public async Task<ActionResult<ApiResponse<User>>> UpdateUser(int id, [FromBody] UpdateUserDto datos)
        {
            if (id <= 0) return BadRequest(ApiResponse<object>.Error("ID de usuario inválido"));

            int requestorId = User.GetUserId();
            var usuarioActualizado = await _userService.UpdateUserAsync(id, datos, requestorId);

            return Ok(ApiResponse<User>.Exito(usuarioActualizado, "Usuario actualizado correctamente"));
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> ChangePassword([FromBody] ChangePasswordDto datos)
        {
            int userId = User.GetUserId();
            await _userService.ChangePasswordAsync(userId, datos);

            return Ok(ApiResponse<object>.Exito(null, "Contraseña actualizada correctamente. Por favor inicia sesión nuevamente."));
        }

        [HttpPost("admin-reset-password/{id}")]
        [RequierePermiso("users.reset_password")]
        public async Task<ActionResult<ApiResponse<object>>> AdminResetPassword(int id, [FromBody] AdminResetPasswordDto datos)
        {
            await _userService.ResetPasswordByAdminAsync(id, datos.NewPassword);
            return Ok(ApiResponse<object>.Exito(null, $"La contraseña del usuario {id} ha sido restablecida exitosamente."));
        }

        [HttpGet]
        [RequierePermiso("view.users")]
        public async Task<ActionResult<ApiResponse<PagedResponse<UserDto>>>> GetAll(
            [FromQuery] bool isActive = true,
            [FromQuery] int? rolId = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            int requestorId = User.GetUserId();
            var resultado = await _userService.GetAllUsersAsync(requestorId, isActive, rolId, pageNumber, pageSize);
            return Ok(ApiResponse<PagedResponse<UserDto>>.Exito(resultado));
        }

        [HttpGet("search/{termino}")]
        [RequierePermiso("view.users")]
        public async Task<ActionResult<ApiResponse<List<UserDto>>>> Search(
            string termino,
            [FromQuery] bool isActive = true,
            [FromQuery] int? rolId = null)
        {
            int requestorId = User.GetUserId();
            var resultados = await _userService.SearchUsersAsync(termino, requestorId, isActive, rolId);
            return Ok(ApiResponse<List<UserDto>>.Exito(resultados));
        }

        [HttpDelete("delete/{id}")]
        [RequierePermiso("delete.users")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteUser(int id)
        {
            int requestorId = User.GetUserId();

            if (id == requestorId)
            {
                return BadRequest(ApiResponse<object>.Error("No puedes eliminar tu propia cuenta."));
            }

            await _userService.DeleteUserAsync(id, requestorId);
            return Ok(ApiResponse<object>.Exito(null, "Usuario eliminado correctamente del sistema."));
        }
    }
}