using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Services; // Asegúrate que aquí está tu PermissionService
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace Api_Tlapaleria.Attributes
{
    public class RequierePermisoAttribute : TypeFilterAttribute
    {
        // 1. Usamos 'params string[]' para poder recibir 1, 2 o más permisos por separado
        public RequierePermisoAttribute(params string[] permisos) : base(typeof(RequierePermisoFilter))
        {
            Arguments = new object[] { permisos };
        }
    }

    public class RequierePermisoFilter : IAsyncAuthorizationFilter
    {
        // 2. Cambiamos la variable para que guarde un arreglo de strings
        private readonly string[] _permisosRequeridos;
        private readonly PermissionService _permissionService;

        public RequierePermisoFilter(string[] permisosRequeridos, PermissionService permissionService)
        {
            _permisosRequeridos = permisosRequeridos;
            _permissionService = permissionService;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (!user.Identity.IsAuthenticated)
            {
                context.Result = new UnauthorizedObjectResult(ApiResponse<object>.Error("Usuario no autenticado"));
                return;
            }

            // Obtenemos el Rol del Token (ej: "Vendedor")
            var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

            // --- 3. AQUÍ ESTÁ LA MAGIA DEL "OR" ---
            bool tieneAlMenosUnPermiso = false;

            // Iteramos por todos los permisos que mandaste en el controlador
            foreach (var permiso in _permisosRequeridos)
            {
                // Con uno solo que regrese 'true' en la BD, rompemos el ciclo y le damos acceso
                if (await _permissionService.UserHasPermissionAsync(userRole, permiso))
                {
                    tieneAlMenosUnPermiso = true;
                    break;
                }
            }

            // Si después de revisar todos los permisos ninguno fue válido, bloqueamos el paso
            if (!tieneAlMenosUnPermiso)
            {
                var listaPermisos = string.Join(" o ", _permisosRequeridos);
                context.Result = new ObjectResult(ApiResponse<object>.Error($"No tienes permisos suficientes. Requieres: {listaPermisos}"))
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}