using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Services; // Asegúrate que aquí está tu PermissionService
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace Api_Tlapaleria.Attributes
{
    public class RequierePermisoAttribute : TypeFilterAttribute
    {
        public RequierePermisoAttribute(string permiso) : base(typeof(RequierePermisoFilter))
        {
            Arguments = new object[] { permiso };
        }
    }

    public class RequierePermisoFilter : IAsyncAuthorizationFilter
    {
        private readonly string _permisoRequerido;
        private readonly PermissionService _permissionService;

        public RequierePermisoFilter(string permisoRequerido, PermissionService permissionService)
        {
            _permisoRequerido = permisoRequerido;
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

            // Obtenemos el Rol del Token (que viene como nombre, ej: "Vendedor")
            var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

            // Consultamos a la BD si ese rol tiene el permiso
            var tienePermiso = await _permissionService.UserHasPermissionAsync(userRole, _permisoRequerido);

            if (!tienePermiso)
            {
                context.Result = new ObjectResult(ApiResponse<object>.Error($"No tienes permisos para: {_permisoRequerido}"))
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}