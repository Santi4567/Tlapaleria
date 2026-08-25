using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Api_Tlapaleria.Services;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services // Nota: Asegúrate de que coincida con tu carpeta (Service o Services)
{
    public class RoleService : IRoleService
    {
        private readonly TlapaleriaContext _context;

        public RoleService(TlapaleriaContext context)
        {
            _context = context;
        }

        // 1. OBTENER TODOS LOS ROLES (Con sus permisos mapeados)
        public async Task<List<RolDto>> GetAllRolesAsync()
        {
            var roles = await _context.Roles
                .AsNoTracking()
                .Include(r => r.Permisos)
                .OrderBy(r => r.Id)
                .ToListAsync();

            return roles.Select(r => new RolDto
            {
                Id = r.Id,
                Nombre = r.Nombre,
                PermisosIds = r.Permisos.Select(p => p.Id).ToList(),
                PermisosNombres = r.Permisos.Select(p => p.NombreSistema).ToList()
            }).ToList();
        }

        // 2. OBTENER EL CATÁLOGO COMPLETO DE PERMISOS (Para los checkboxes en React)
        public async Task<List<Permiso>> GetAllPermissionsAsync()
        {
            return await _context.Permisos
                .AsNoTracking()
                .OrderBy(p => p.NombreSistema)
                .ToListAsync();
        }

        // 3. OBTENER UN ROL POR ID
        public async Task<RolDto> GetRoleByIdAsync(int id)
        {
            var r = await _context.Roles
                .Include(r => r.Permisos)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (r == null) throw new Exception("Rol no encontrado.");

            return new RolDto
            {
                Id = r.Id,
                Nombre = r.Nombre,
                PermisosIds = r.Permisos.Select(p => p.Id).ToList(),
                PermisosNombres = r.Permisos.Select(p => p.NombreSistema).ToList()
            };
        }

        // 4. CREAR NUEVO ROL (Y ligar los permisos seleccionados)
        public async Task<RolDto> CreateRoleAsync(CreateEditRolDto datos)
        {
            // Validamos que no exista otro rol con el mismo nombre exacto
            if (await _context.Roles.AnyAsync(r => r.Nombre == datos.Nombre))
                throw new Exception($"El rol '{datos.Nombre}' ya existe.");

            var nuevoRol = new Rol { Nombre = datos.Nombre };

            // --- MAGIA DE UNIÓN MUCHOS A MUCHOS ---
            // Buscamos los permisos por ID y se los asignamos al rol
            if (datos.PermisosIds.Any())
            {
                var permisosBD = await _context.Permisos
                    .Where(p => datos.PermisosIds.Contains(p.Id))
                    .ToListAsync();

                nuevoRol.Permisos = permisosBD; // EF Core creará la unión automáticamente
            }

            _context.Roles.Add(nuevoRol);
            await _context.SaveChangesAsync();

            return await GetRoleByIdAsync(nuevoRol.Id);
        }

        // 5. EDITAR ROL (EXCLUSIVO PARA CAMBIAR EL NOMBRE)
        public async Task<RolDto> UpdateRoleAsync(int id, UpdateRolNameDto datos)
        {
            // 1. Buscamos el rol. (Incluimos Permisos solo para poder devolver el RolDto completo al final)
            var rol = await _context.Roles
                .Include(r => r.Permisos)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (rol == null) throw new Exception("Rol no encontrado.");

            // Blindaje 1: Nadie puede renombrar el rol Admin
            if (rol.Nombre == "Admin" && datos.Nombre != "Admin")
                throw new Exception("No puedes cambiarle el nombre al rol principal 'Admin'.");

            // Blindaje 2: Evitar nombres duplicados en la base de datos
            if (await _context.Roles.AnyAsync(r => r.Nombre == datos.Nombre && r.Id != id))
                throw new Exception($"Ya existe otro rol llamado '{datos.Nombre}'.");

            // --- AQUÍ ESTÁ EL CAMBIO LIMPIO ---
            rol.Nombre = datos.Nombre;
            // ¡Ya no tocamos rol.Permisos para absolutamente nada! Sus permisos quedan intactos.

            _context.Roles.Update(rol);
            await _context.SaveChangesAsync();

            return await GetRoleByIdAsync(rol.Id);
        }

        // 6. ELIMINAR ROL (Con protección anti-huérfanos)
        public async Task<bool> DeleteRoleAsync(int id)
        {
            var rol = await _context.Roles.FindAsync(id);
            if (rol == null) throw new Exception("Rol no encontrado.");

            // Blindaje 1: Proteger el rol del sistema
            if (rol.Nombre == "Admin")
                throw new Exception("El rol 'Admin' es del sistema y no puede ser eliminado.");

            // Blindaje 2: Evitar dejar usuarios huérfanos (Tu regla de seguridad elegida)
            bool estaEnUso = await _context.Users.AnyAsync(u => u.RolId == id);
            if (estaEnUso)
                throw new Exception("No se puede eliminar este rol porque hay usuarios que lo tienen asignado. Reasigna o desactiva a esos usuarios primero.");

            _context.Roles.Remove(rol);
            await _context.SaveChangesAsync();
            return true;
        }

        // 7. OBTENER USUARIOS POR ROL CON PAGINACIÓN
        public async Task<PagedResponse<UserDto>> GetUsersByRoleIdAsync(int rolId, int pageNumber = 1, int pageSize = 10)
        {
            // 1. Límites de seguridad en la paginación
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100; // Blindaje: Máximo 100 registros por página

            // 2. Validar que el rol realmente exista en la BD
            bool rolExiste = await _context.Roles.AnyAsync(r => r.Id == rolId);
            if (!rolExiste) throw new Exception("El rol especificado no existe en el sistema.");

            // 3. Consulta base sin tracking para máxima velocidad
            var query = _context.Users
                .AsNoTracking()
                .Include(u => u.Rol)
                .Where(u => u.RolId == rolId)
                .AsQueryable();

            // 4. Conteo total real en la base de datos
            var totalItems = await query.CountAsync();

            // 5. Aplicar paginación (LIMIT y OFFSET en MariaDB)
            var usuarios = await query
                .OrderBy(u => u.Name) // Orden constante para paginación consistente
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Username = u.Username,
                    Rol = u.Rol.Nombre,
                    IsActive = u.IsActive
                })
                .ToListAsync();

            // 6. Calcular total de páginas
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            // 7. Retornar DTO paginado
            return new PagedResponse<UserDto>
            {
                Data = usuarios,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };
        }
        
        public async Task<List<RolDto>> SearchRolesAsync(string termino)
        {
            if (string.IsNullOrWhiteSpace(termino)) return new List<RolDto>();

            var roles = await _context.Roles
                .AsNoTracking()
                .Include(r => r.Permisos)
                .Where(r => r.Nombre.Contains(termino))
                .OrderBy(r => r.Nombre)
                .Take(10)
                .ToListAsync();

            return roles.Select(r => new RolDto
            {
                Id = r.Id,
                Nombre = r.Nombre,
                PermisosIds = r.Permisos.Select(p => p.Id).ToList(),
                PermisosNombres = r.Permisos.Select(p => p.NombreSistema).ToList()
            }).ToList();
        }

        // =====================================================================
        // ASIGNAR UN PERMISO A UN ROL (INSERT en tabla intermedia)
        // =====================================================================
        public async Task<RolDto> AssignPermissionAsync(int rolId, int permisoId)
        {
            // 1. Buscamos el rol INCLUYENDO sus permisos actuales para poder evaluar la lista
            var rol = await _context.Roles
                .Include(r => r.Permisos)
                .FirstOrDefaultAsync(r => r.Id == rolId);

            if (rol == null) throw new Exception("Rol no encontrado.");

            // 2. Verificamos si el rol ya tiene asignado ese permiso (Evitar duplicados)
            if (rol.Permisos.Any(p => p.Id == permisoId))
            {
                // Si ya lo tiene, simplemente retornamos el rol sin hacer cambios
                return await GetRoleByIdAsync(rolId);
            }

            // 3. Buscamos que el permiso realmente exista en el catálogo del sistema
            var permiso = await _context.Permisos.FindAsync(permisoId);
            if (permiso == null) throw new Exception("El permiso especificado no existe.");

            // 4. MAGIA DE EF CORE: Agregamos a la lista y guardamos
            rol.Permisos.Add(permiso);
            await _context.SaveChangesAsync(); // SQL ejecuta: INSERT INTO RolPermiso (RolId, PermisoId) VALUES (...)

            return await GetRoleByIdAsync(rolId);
        }

        // =====================================================================
        // REMOVER UN PERMISO DE UN ROL (DELETE en tabla intermedia)
        // =====================================================================
        public async Task<RolDto> RemovePermissionAsync(int rolId, int permisoId)
        {
            // 1. Buscamos el rol INCLUYENDO sus permisos actuales
            var rol = await _context.Roles
                .Include(r => r.Permisos)
                .FirstOrDefaultAsync(r => r.Id == rolId);

            if (rol == null) throw new Exception("Rol no encontrado.");

            // --- BLINDAJE ANTI-BLOQUEO ---
            // Evitamos que por accidente le quiten herramientas al rol principal del sistema
            if (rol.Nombre == "Admin")
            {
                throw new Exception("Por seguridad del sistema, no se pueden remover permisos del rol 'Admin'.");
            }

            // 2. Buscamos si el rol actualmente tiene ese permiso en su lista
            var permisoARemover = rol.Permisos.FirstOrDefault(p => p.Id == permisoId);

            // Si no lo tiene, retornamos el rol tal como está
            if (permisoARemover == null)
            {
                return await GetRoleByIdAsync(rolId);
            }

            // 3. MAGIA DE EF CORE: Removemos de la lista y guardamos
            rol.Permisos.Remove(permisoARemover);
            await _context.SaveChangesAsync(); // SQL ejecuta: DELETE FROM RolPermiso WHERE RolId = x AND PermisoId = y

            return await GetRoleByIdAsync(rolId);
        }

        //-------------------------------------------------------------------------------------------------------------------------------

        // =====================================================================
        // ASIGNAR MULTIPLES PERMISOS A UN ROL (BULK)
        // =====================================================================
        public async Task<RolDto> AssignMultiplePermissionsAsync(int rolId, List<int> permisosIds)
        {
            var rol = await _context.Roles
                .Include(r => r.Permisos)
                .FirstOrDefaultAsync(r => r.Id == rolId);

            if (rol == null) throw new Exception("Rol no encontrado.");

            // Evitar procesar duplicados en la lista de entrada
            var uniqueIds = permisosIds.Distinct().ToList();

            // Validar que TODOS los permisos existan (Caso A)
            var permisosBD = await _context.Permisos
                .Where(p => uniqueIds.Contains(p.Id))
                .ToListAsync();

            if (permisosBD.Count != uniqueIds.Count)
            {
                throw new Exception("Operación rechazada: Uno o más permisos especificados no existen en el sistema.");
            }

            // Agregar solo los que el rol no tenga actualmente
            foreach (var permiso in permisosBD)
            {
                if (!rol.Permisos.Any(p => p.Id == permiso.Id))
                {
                    rol.Permisos.Add(permiso);
                }
            }

            await _context.SaveChangesAsync();
            return await GetRoleByIdAsync(rolId);
        }

        // =====================================================================
        // REMOVER MULTIPLES PERMISOS DE UN ROL (BULK)
        // =====================================================================
        public async Task<RolDto> RemoveMultiplePermissionsAsync(int rolId, List<int> permisosIds)
        {
            var rol = await _context.Roles
                .Include(r => r.Permisos)
                .FirstOrDefaultAsync(r => r.Id == rolId);

            if (rol == null) throw new Exception("Rol no encontrado.");

            if (rol.Nombre == "Admin")
                throw new Exception("Por seguridad del sistema, no se pueden remover permisos del rol 'Admin'.");

            var uniqueIds = permisosIds.Distinct().ToList();

            // Validar que los permisos existan en el catálogo general (Caso A)
            var permisosBDCount = await _context.Permisos
                .Where(p => uniqueIds.Contains(p.Id))
                .CountAsync();

            if (permisosBDCount != uniqueIds.Count)
            {
                throw new Exception("Operación rechazada: Intentas remover uno o más permisos que no existen en el sistema.");
            }

            // Identificar cuáles de esos permisos realmente tiene el rol para removerlos
            var permisosARemover = rol.Permisos.Where(p => uniqueIds.Contains(p.Id)).ToList();

            foreach (var permiso in permisosARemover)
            {
                rol.Permisos.Remove(permiso);
            }

            await _context.SaveChangesAsync();
            return await GetRoleByIdAsync(rolId);
        }
    }
}