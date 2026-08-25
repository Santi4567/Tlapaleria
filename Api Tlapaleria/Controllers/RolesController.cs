using Api_Tlapaleria.Attributes;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Api_Tlapaleria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Tlapaleria.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RolesController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        [HttpGet]
        [RequierePermiso("view.roles")]
        public async Task<ActionResult<ApiResponse<List<RolDto>>>> GetAll()
        {
            var lista = await _roleService.GetAllRolesAsync();
            return Ok(ApiResponse<List<RolDto>>.Exito(lista));
        }

        [HttpGet("permissions")]
        [RequierePermiso("view.roles")]
        public async Task<ActionResult<ApiResponse<List<Permiso>>>> GetAllPermissions()
        {
            var lista = await _roleService.GetAllPermissionsAsync();
            return Ok(ApiResponse<List<Permiso>>.Exito(lista));
        }

        [HttpGet("search/{termino}")]
        [RequierePermiso("view.roles")]
        public async Task<ActionResult<ApiResponse<List<RolDto>>>> Search(string termino)
        {
            var resultados = await _roleService.SearchRolesAsync(termino);
            return Ok(ApiResponse<List<RolDto>>.Exito(resultados));
        }

        [HttpGet("{id}")]
        [RequierePermiso("view.roles")]
        public async Task<ActionResult<ApiResponse<RolDto>>> GetById(int id)
        {
            var rol = await _roleService.GetRoleByIdAsync(id);
            return Ok(ApiResponse<RolDto>.Exito(rol));
        }

        [HttpGet("{id}/users")]
        public async Task<ActionResult<ApiResponse<PagedResponse<UserDto>>>> GetUsersByRole(
            int id,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromServices] PermissionService permissionService = null!)
        {
            var rolSolicitante = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            bool esAdmin = rolSolicitante == "Admin";
            bool tienePermiso = await permissionService.UserHasPermissionAsync(rolSolicitante, "user.privilege_view");

            if (!esAdmin && !tienePermiso)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Error("Acceso denegado. Requieres ser Administrador o tener el permiso 'user.privilege_view' para ver el personal asignado a este rol."));
            }

            var resultado = await _roleService.GetUsersByRoleIdAsync(id, pageNumber, pageSize);
            return Ok(ApiResponse<PagedResponse<UserDto>>.Exito(resultado));
        }

        [HttpPost]
        [RequierePermiso("add.roles")]
        public async Task<ActionResult<ApiResponse<RolDto>>> Create([FromBody] CreateEditRolDto datos)
        {
            var creado = await _roleService.CreateRoleAsync(datos);
            return Ok(ApiResponse<RolDto>.Exito(creado, "Rol creado y permisos asignados exitosamente"));
        }

        [HttpPut("{id}")]
        [RequierePermiso("edit.roles")]
        public async Task<ActionResult<ApiResponse<RolDto>>> Update(int id, [FromBody] UpdateRolNameDto datos)
        {
            var actualizado = await _roleService.UpdateRoleAsync(id, datos);
            return Ok(ApiResponse<RolDto>.Exito(actualizado, "Nombre del rol actualizado exitosamente"));
        }

        [HttpDelete("{id}")]
        [RequierePermiso("delete.roles")]
        public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
        {
            await _roleService.DeleteRoleAsync(id);
            return Ok(ApiResponse<object>.Exito(null, "Rol eliminado correctamente del sistema"));
        }

        //AGREGAR UN SOLO PERMISOS ala vez
        [HttpPost("{rolId}/permissions/{permisoId}")]
        [RequierePermiso("edit.roles")]
        public async Task<ActionResult<ApiResponse<RolDto>>> AssignPermission(int rolId, int permisoId)
        {
            var actualizado = await _roleService.AssignPermissionAsync(rolId, permisoId);
            return Ok(ApiResponse<RolDto>.Exito(actualizado, "Permiso asignado correctamente al rol"));
        }

        //ELIMINAR UNO SOLO permiso a la vez
        [HttpDelete("{rolId}/permissions/{permisoId}")]
        [RequierePermiso("edit.roles")]
        public async Task<ActionResult<ApiResponse<RolDto>>> RemovePermission(int rolId, int permisoId)
        {
            var actualizado = await _roleService.RemovePermissionAsync(rolId, permisoId);
            return Ok(ApiResponse<RolDto>.Exito(actualizado, "Permiso removido correctamente del rol"));
        }

        // AGREAGR VARIOS PERMISOS A LA VEZ 

        [HttpPost("{rolId}/permissions/bulk")]
        [RequierePermiso("edit.roles")]
        public async Task<ActionResult<ApiResponse<RolDto>>> AssignMultiplePermissions(int rolId, [FromBody] List<int> permisosIds)
        {
            var actualizado = await _roleService.AssignMultiplePermissionsAsync(rolId, permisosIds);
            return Ok(ApiResponse<RolDto>.Exito(actualizado, "Permisos asignados correctamente al rol de forma masiva"));
        }


        // ELIMINAR VARIOS PERMISOS A LA VEZ 
        [HttpDelete("{rolId}/permissions/bulk")]
        [RequierePermiso("edit.roles")]
        public async Task<ActionResult<ApiResponse<RolDto>>> RemoveMultiplePermissions(int rolId, [FromBody] List<int> permisosIds)
        {
            var actualizado = await _roleService.RemoveMultiplePermissionsAsync(rolId, permisosIds);
            return Ok(ApiResponse<RolDto>.Exito(actualizado, "Permisos removidos correctamente del rol de forma masiva"));
        }
    }
}