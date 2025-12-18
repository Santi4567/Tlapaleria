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
        [RequierePermiso("add.users")] // 2. Solo pueden crear usuarios los que que tengan este permiso 
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
        [RequierePermiso("edit.users")] // Solo quien tenga permiso de editar usuarios puede entrar aquí
        public async Task<ActionResult<ApiResponse<User>>> UpdateUser(int id, [FromBody] UpdateUserDto datos)
        {
            try
            {
                // Validamos que el ID de la URL coincida con la lógica (aunque aquí es redundante, es buena práctica)
                if (id <= 0) return BadRequest(ApiResponse<object>.Error("ID de usuario inválido"));

                var usuarioActualizado = await _userService.UpdateUserAsync(id, datos);

                return Ok(ApiResponse<User>.Exito(usuarioActualizado, "Usuario actualizado correctamente"));
            }
            catch (Exception ex)
            {
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

        // 1. OBTENER TODOS LOS USUARIOS
        [HttpGet] // GET: api/users
        [RequierePermiso("view.users")]
        public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetAll()
        {
            try
            {
                var lista = await _userService.GetAllUsersAsync();
                return Ok(ApiResponse<List<UserDto>>.Exito(lista));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        // 2. BUSCAR USUARIOS
        [HttpGet("search/{termino}")] // GET: api/users/search/juan
        [RequierePermiso("view.users")]
        public async Task<ActionResult<ApiResponse<List<UserDto>>>> Search(string termino)
        {
            try
            {
                var resultados = await _userService.SearchUsersAsync(termino);

                if (resultados.Count == 0)
                {
                    // Opcional: Puedes devolver Exito con lista vacía, o un mensaje
                    return Ok(ApiResponse<List<UserDto>>.Exito(resultados, "No se encontraron coincidencias."));
                }

                return Ok(ApiResponse<List<UserDto>>.Exito(resultados));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
    }
};