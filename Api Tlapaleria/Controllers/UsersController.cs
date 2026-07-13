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

        //Crear Nuevo Usuario 
        [HttpPost("create")]
        [RequierePermiso("add.users")] // Permiso necesario 
        public async Task<ActionResult<ApiResponse<User>>> CreateUser([FromBody] RegisterUserDto datos)
        {
            try
            {
                // 1. OBTENER QUIÉN ESTÁ HACIENDO LA PETICIÓN
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (idClaim == null) return Unauthorized(ApiResponse<object>.Error("Token inválido"));

                int requestorId = int.Parse(idClaim.Value);

                // 2. LLAMAR AL SERVICIO CON EL ID DEL CREADOR
                // Si un Gerente intenta crear un Admin, el servicio lanzará la excepción aquí.
                var usuarioCreado = await _userService.RegisterAsync(datos, requestorId);

                return Ok(ApiResponse<User>.Exito(usuarioCreado, "Usuario registrado correctamente"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        //Obtener datos del perfil de Usuario 
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

        //Actualizar Usuarios 
        [HttpPut("update/{id}")]
        [RequierePermiso("edit.users")]
        public async Task<ActionResult<ApiResponse<User>>> UpdateUser(int id, [FromBody] UpdateUserDto datos)
        {
            try
            {
                if (id <= 0) return BadRequest(ApiResponse<object>.Error("ID de usuario inválido"));

                // 1. OBTENER QUIÉN ESTÁ HACIENDO LA PETICIÓN
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (idClaim == null) return Unauthorized(ApiResponse<object>.Error("Token inválido"));

                int requestorId = int.Parse(idClaim.Value);

                // 2. LLAMAR AL SERVICIO CON EL ID DEL EJECUTOR
                var usuarioActualizado = await _userService.UpdateUserAsync(id, datos, requestorId);

                return Ok(ApiResponse<User>.Exito(usuarioActualizado, "Usuario actualizado correctamente"));
            }
            catch (Exception ex)
            {
                // Aquí caerán los errores de permisos insuficientes
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        //Cambio de contrasena 
        [HttpPost("change-password")]
        [Authorize] // Cualquier usuario con token válido puede entrar aquí
        public async Task<ActionResult<ApiResponse<object>>> ChangePassword([FromBody] ChangePasswordDto datos)
        {
            try
            {
                // 1. OBTENER ID DEL TOKEN (Seguridad: Nadie puede cambiar la pass de otro)
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (idClaim == null) return Unauthorized(ApiResponse<object>.Error("Token inválido"));

                int userId = int.Parse(idClaim.Value);

                // 2. LLAMAR AL SERVICIO
                await _userService.ChangePasswordAsync(userId, datos);

                return Ok(ApiResponse<object>.Exito(null, "Contraseña actualizada correctamente. Por favor inicia sesión nuevamente."));
            }
            catch (Exception ex)
            {
                // Aquí caerá si la contraseña actual estaba mal o si la nueva es igual a la anterior
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        //Cambio de contrasena por un admin 
        [HttpPost("admin-reset-password/{id}")]
        [RequierePermiso("users.reset_password")] // <--- SOLO ADMIN O GERENTES CON PERMISO
        public async Task<ActionResult<ApiResponse<object>>> AdminResetPassword(int id, [FromBody] AdminResetPasswordDto datos)
        {
            try
            {
                // Validamos que no se intenten resetear a sí mismos por este medio (opcional, pero buena práctica)
                // var requesterId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                // if (requesterId == id) throw new Exception("Para cambiar tu propia contraseña usa el otro endpoint.");

                await _userService.ResetPasswordByAdminAsync(id, datos.NewPassword);

                return Ok(ApiResponse<object>.Exito(null, $"La contraseña del usuario {id} ha sido restablecida exitosamente."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        // 1. OBTENER TODOS LOS USUARIOS (Paginados y con filtros dinámicos)
        [HttpGet]
        [RequierePermiso("view.users")]
        public async Task<ActionResult<ApiResponse<PagedResponse<UserDto>>>> GetAll(
            [FromQuery] bool isActive = true,
            [FromQuery] int? rolId = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                // 1. Obtener ID del solicitante desde el token
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (idClaim == null) return Unauthorized(ApiResponse<object>.Error("Token inválido"));
                int requestorId = int.Parse(idClaim.Value);

                // 2. Llamar al servicio pasándole todos los filtros
                var resultado = await _userService.GetAllUsersAsync(requestorId, isActive, rolId, pageNumber, pageSize);

                return Ok(ApiResponse<PagedResponse<UserDto>>.Exito(resultado));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        // 2. BUSCAR USUARIOS POR TÉRMINO, ESTADO Y ROL OPCIONAL
        // Ejemplo Normal (Activos): /api/users/search/carlos
        // Ejemplo Inactivos: /api/users/search/carlos?isActive=false
        // Ejemplo Cajeros Activos: /api/users/search/carlos?rolId=2
        // Ejemplo Combo completo: /api/users/search/carlos?isActive=false&rolId=2
        [HttpGet("search/{termino}")]
        [RequierePermiso("view.users")]
        public async Task<ActionResult<ApiResponse<List<UserDto>>>> Search(
            string termino,
            [FromQuery] bool isActive = true,
            [FromQuery] int? rolId = null)
        {
            try
            {
                // 1. Obtener ID del solicitante desde el token
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (idClaim == null) return Unauthorized(ApiResponse<object>.Error("Token inválido"));
                int requestorId = int.Parse(idClaim.Value);

                // 2. Llamar al servicio pasándole todos los filtros
                var resultados = await _userService.SearchUsersAsync(termino, requestorId, isActive, rolId);

                return Ok(ApiResponse<List<UserDto>>.Exito(resultados));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        //Eliminar 
        [HttpDelete("delete/{id}")]
        [RequierePermiso("delete.users")] // Validamos que tenga el permiso base primero
        public async Task<ActionResult<ApiResponse<object>>> DeleteUser(int id)
        {
            try
            {
                // 1. OBTENER ID DEL EJECUTOR (Desde el Token)
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (idClaim == null) return Unauthorized(ApiResponse<object>.Error("Token inválido"));

                int requestorId = int.Parse(idClaim.Value);

                // 2. EVITAR SUICIDIO (Opcional pero recomendado)
                if (id == requestorId)
                {
                    return BadRequest(ApiResponse<object>.Error("No puedes eliminar tu propia cuenta."));
                }

                // 3. LLAMAR AL SERVICIO
                await _userService.DeleteUserAsync(id, requestorId);

                return Ok(ApiResponse<object>.Exito(null, "Usuario eliminado correctamente del sistema."));
            }
            catch (Exception ex)
            {
                // Aquí caerán los errores de "No eres admin" o "Es el último admin"
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
    }
};