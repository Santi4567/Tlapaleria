using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IRoleService
    {
        Task<List<RolDto>> GetAllRolesAsync();
        Task<List<Permiso>> GetAllPermissionsAsync(); // Para dibujar checkboxes en React
        Task<RolDto> GetRoleByIdAsync(int id);
        Task<RolDto> CreateRoleAsync(CreateEditRolDto datos);

        //Actualizar Nombre del Rol
        Task<RolDto> UpdateRoleAsync(int id, UpdateRolNameDto datos);
        Task<bool> DeleteRoleAsync(int id);

        Task<List<RolDto>> SearchRolesAsync(string termino);
        Task<PagedResponse<UserDto>> GetUsersByRoleIdAsync(int rolId, int pageNumber = 1, int pageSize = 10);

        //Asignar permisos
        Task<RolDto> AssignPermissionAsync(int rolId, int permisoId);
        //Remover Permisos 
        Task<RolDto> RemovePermissionAsync(int rolId, int permisoId);

        // Asiganar permisos en forma de lista
        Task<RolDto> AssignMultiplePermissionsAsync(int rolId, List<int> permisosIds);

        // Eliminar permisos en forma de lista

        Task<RolDto> RemoveMultiplePermissionsAsync(int rolId, List<int> permisosIds);
    }
}
