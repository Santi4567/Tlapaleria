using Api_Tlapaleria.Data;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class PermissionService
    {
        // Usamos IServiceScopeFactory porque este servicio podría ser Singleton
        // y el DbContext es Scoped (tiene vidas diferentes).
        private readonly IServiceScopeFactory _scopeFactory;

        public PermissionService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<bool> UserHasPermissionAsync(string rolNombre, string permisoRequerido)
        {
            if (rolNombre == "Admin") return true; // El Admin siempre pasa

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TlapaleriaContext>();

                // CONSULTA SQL EFICIENTE:
                // "Busca si existe algún Rol con este nombre...
                //  ...que tenga en su lista de Permisos...
                //  ...alguno que coincida con el permisoRequerido"

                bool tienePermiso = await context.Roles
                    .Where(r => r.Nombre == rolNombre)
                    .AnyAsync(r => r.Permisos.Any(p => p.NombreSistema == permisoRequerido));

                return tienePermiso;
            }
        }
    }
}