using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class UserService : IUserService
    {
        private readonly TlapaleriaContext _context;

        public UserService(TlapaleriaContext context)
        {
            _context = context;
        }

        //Registrar Usuarios 
        public async Task<User> RegisterAsync(RegisterUserDto datos, int requestorId)
        {
            // 1. VALIDACIÓN DE DUPLICADOS (Username)
            var existe = await _context.Users.AnyAsync(u => u.Username == datos.Username);
            if (existe) throw new Exception($"El usuario '{datos.Username}' ya existe.");

            // 2. BUSCAR EL ROL QUE SE QUIERE ASIGNAR
            var rolObjetivo = await _context.Roles
                .FirstOrDefaultAsync(r => r.Nombre == datos.RolNombre);

            if (rolObjetivo == null) throw new Exception($"El rol '{datos.RolNombre}' no es válido.");

            // --- REGLA DE PROTECCIÓN DE ADMINS (AQUÍ ESTÁ LO NUEVO) ---
            if (rolObjetivo.Nombre == "Admin")
            {
                // Buscamos quién está haciendo la petición
                var creador = await _context.Users
                    .Include(u => u.Rol)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == requestorId);

                // Si el creador no existe (raro) o SU ROL no es Admin...
                if (creador == null || creador.Rol.Nombre != "Admin")
                {
                    throw new Exception("Acceso Denegado: Solo un Administrador puede crear cuentas con rol 'Admin'.");
                }
            }
            // -----------------------------------------------------------

            // 3. Encriptar contraseña
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(datos.Password);

            // 4. Crear el usuario
            var nuevoUsuario = new User
            {
                Username = datos.Username,
                Passwd = passwordHash,
                Name = datos.Name,
                RolId = rolObjetivo.Id,
                IsActive = false // Se crea desactivado por seguridad, como acordamos
            };

            _context.Users.Add(nuevoUsuario);
            await _context.SaveChangesAsync();

            // 5. Cargar datos visuales
            await _context.Entry(nuevoUsuario).Reference(u => u.Rol).LoadAsync();

            return nuevoUsuario;
        }

        //Obtener Datos del Usuario, Nombre,rol, permisos 

        public async Task<UserProfileDto> GetUserProfileAsync(int userId)
        {
            // 1. Buscamos al usuario y TRAEMOS TODA LA FAMILIA (Rol y Permisos)
            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.Rol)                // Trae el Rol
                    .ThenInclude(r => r.Permisos)   // Trae los Permisos de ese Rol
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) throw new Exception("Usuario no encontrado");

            // 2. LOGICA DE AGRUPAMIENTO (El truco)
            // Suponemos que tus permisos son "verbo.entidad" (ej: "add.users")
            // Usamos la "entidad" (users) como nombre del grupo.

            var permisosAgrupados = user.Rol.Permisos
                .GroupBy(p => {
                    // Truco: Dividimos "add.users" por el punto y tomamos la última parte ("users")
                    // Si el permiso no tiene puntos, se usa el nombre completo.
                    var partes = p.NombreSistema.Split('.');
                    return partes.Length > 1 ? partes.Last() : "General";
                })
                .ToDictionary(
                    grupo => grupo.Key.ToUpper(), // La Clave: "USERS", "PRODUCTS"
                    grupo => grupo.Select(p => p.NombreSistema).ToList() // La Lista: ["add.users", "edit.users"]
                );

            // 3. Mapeamos al DTO
            return new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                Name = user.Name,
                Rol = user.Rol.Nombre,
                Permisos = permisosAgrupados
            };
        }
        // Actualizar usuario

        public async Task<User> UpdateUserAsync(int id, UpdateUserDto datos, int requestorId)
        {
            // 1. BUSCAR A LA VÍCTIMA (Target)
            var user = await _context.Users
                .Include(u => u.Rol) // Necesitamos el Rol actual
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) throw new Exception("Usuario no encontrado.");

            // 2. BUSCAR AL EJECUTOR (Requestor)
            var requestorUser = await _context.Users
                .AsNoTracking() // Solo leemos
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Id == requestorId);

            if (requestorUser == null) throw new Exception("Usuario ejecutor no válido.");

            // --- REGLA 1: PROTECCIÓN DE RANGO (NO TOCAR ADMINS) ---
            // Si intentas editar a un Admin... y TÚ NO eres Admin... ¡ERROR!
            if (user.Rol.Nombre == "Admin" && requestorUser.Rol.Nombre != "Admin")
            {
                throw new Exception("No tienes rango suficiente para editar a un Administrador.");
            }

            // 3. VALIDAR EL NUEVO ROL (Anti-Escalada)
            // Buscamos el ID del rol nuevo que mandaron en el JSON
            var rolNuevo = await _context.Roles
                .FirstOrDefaultAsync(r => r.Nombre == datos.RolNombre);

            if (rolNuevo == null) throw new Exception($"El rol '{datos.RolNombre}' no existe.");

            // --- REGLA 2: ANTI-ESCALADA (NO CREAR ADMINS) ---
            // Si intentas ponerle el rol "Admin" a alguien... y TÚ NO eres Admin... ¡ERROR!
            if (rolNuevo.Nombre == "Admin" && requestorUser.Rol.Nombre != "Admin")
            {
                throw new Exception("Acceso Denegado: Solo un Administrador puede asignar el rol 'Admin'.");
            }

            // 4. VALIDAR DUPLICADOS DE USERNAME
            // (Igual que antes, pero asegurándonos de excluir al propio usuario)
            bool nombreOcupado = await _context.Users
                .AnyAsync(u => u.Username == datos.Username && u.Id != id);

            if (nombreOcupado) throw new Exception($"El usuario '{datos.Username}' ya está en uso.");


            // 5. APLICAR CAMBIOS
            user.Name = datos.Name;
            user.Username = datos.Username;
            user.RolId = rolNuevo.Id; // Asignamos el ID seguro
            user.IsActive = datos.IsActive;

            // 6. GUARDAR
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Recargamos la relación para devolver el objeto completo
            await _context.Entry(user).Reference(u => u.Rol).LoadAsync();

            return user;
        }

        //Cambio de contrasena 
        public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto datos)
        {
            // 1. Buscamos al usuario
            var user = await _context.Users.FindAsync(userId);
            if (user == null) throw new Exception("Usuario no encontrado.");

            // 2. VERIFICAR LA CONTRASEÑA ACTUAL
            // Comparamos lo que escribió el usuario vs el Hash que tenemos en la BD
            bool passwordCorrecto = BCrypt.Net.BCrypt.Verify(datos.CurrentPassword, user.Passwd);

            if (!passwordCorrecto)
            {
                throw new Exception("La contraseña actual es incorrecta.");
            }

            // 3. Validar que la nueva no sea igual a la anterior (Opcional, pero buena práctica)
            if (datos.CurrentPassword == datos.NewPassword)
            {
                throw new Exception("La nueva contraseña no puede ser igual a la anterior.");
            }

            // 4. ENCRIPTAR LA NUEVA CONTRASEÑA
            string nuevoHash = BCrypt.Net.BCrypt.HashPassword(datos.NewPassword);

            // 5. Guardar cambios
            user.Passwd = nuevoHash;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return true;
        }

        //Metodo para que un admin pueda camabiar la contrasena de un usuario 
        public async Task<bool> ResetPasswordByAdminAsync(int targetUserId, string newPassword)
        {
            // 1. Buscamos al usuario VICTIMA (al que se la vamos a cambiar)
            var user = await _context.Users.FindAsync(targetUserId);
            if (user == null) throw new Exception("El usuario especificado no existe.");

            // 2. ENCRIPTAR LA NUEVA CONTRASEÑA DIRECTAMENTE
            string nuevoHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

            // 3. Guardar cambios
            user.Passwd = nuevoHash;

            // Opcional: Podrías forzar que IsActive sea true si estaba bloqueado
            // user.IsActive = true; 

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return true;
        }

        //Traer tpdos los usuarios 
        public async Task<List<UserDto>> GetAllUsersAsync(int requestorId)
        {
            // 1. AVERIGUAR QUIÉN PIDE LA LISTA
            var requestor = await _context.Users
                .AsNoTracking()
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Id == requestorId);

            if (requestor == null) throw new Exception("Usuario solicitante no válido.");

            // 2. PREPARAR LA CONSULTA BASE
            var query = _context.Users
                .AsNoTracking()
                .Include(u => u.Rol)
                .AsQueryable(); // Importante: AsQueryable permite agregar filtros paso a paso

            // 3. APLICAR FILTRO DE VISIBILIDAD
            // Si el que pide la lista NO ES ADMIN...
            if (requestor.Rol.Nombre != "Admin")
            {
                // ... filtramos para que NO aparezcan los Admins en la lista
                query = query.Where(u => u.Rol.Nombre != "Admin");
            }

            // 4. EJECUTAR Y PROYECTAR (Select)
            var usuarios = await query
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Username = u.Username,
                    Rol = u.Rol.Nombre,
                    IsActive = u.IsActive
                })
                .ToListAsync();

            return usuarios;
        }

        public async Task<List<UserDto>> SearchUsersAsync(string termino, int requestorId)
        {
            if (string.IsNullOrWhiteSpace(termino)) return new List<UserDto>();

            // 1. AVERIGUAR QUIÉN BUSCA
            var requestor = await _context.Users
                .AsNoTracking()
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Id == requestorId);

            if (requestor == null) throw new Exception("Usuario solicitante no válido.");

            // 2. PREPARAR CONSULTA BASE CON EL TÉRMINO DE BÚSQUEDA
            var query = _context.Users
                .AsNoTracking()
                .Include(u => u.Rol)
                .Where(u => u.Name.Contains(termino) || u.Username.Contains(termino)) // Filtro por nombre
                .AsQueryable();

            // 3. APLICAR FILTRO DE VISIBILIDAD
            // Si no es Admin, ocultamos a los Admins de los resultados de búsqueda
            if (requestor.Rol.Nombre != "Admin")
            {
                query = query.Where(u => u.Rol.Nombre != "Admin");
            }

            // 4. EJECUTAR
            var usuarios = await query
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Username = u.Username,
                    Rol = u.Rol.Nombre,
                    IsActive = u.IsActive
                })
                .ToListAsync();

            return usuarios;
        }

        //Eliminar usuarios 
        public async Task<bool> DeleteUserAsync(int targetUserId, int requestorId)
        {
            // 1. BUSCAR A LA VÍCTIMA (Target)
            var targetUser = await _context.Users
                .Include(u => u.Rol) // Necesitamos saber su rol
                .FirstOrDefaultAsync(u => u.Id == targetUserId);

            if (targetUser == null) throw new Exception("El usuario a eliminar no existe.");

            // 2. BUSCAR AL EJECUTOR (Requestor)
            var requestorUser = await _context.Users
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Id == requestorId);

            if (requestorUser == null) throw new Exception("Usuario ejecutor no válido.");

            // --- REGLA 1: PROTECCIÓN DE RANGO ---
            // "Alguien con permiso delete.users NO puede eliminar admins, solo entre ellos"
            // Si la víctima es Admin... Y el que intenta borrarlo NO es Admin... ¡ERROR!
            if (targetUser.Rol.Nombre == "Admin" && requestorUser.Rol.Nombre != "Admin")
            {
                throw new Exception("No tienes rango suficiente para eliminar a un Administrador.");
            }

            // --- REGLA 2: EL ÚLTIMO HOMBRE EN PIE ---
            // "Siempre debe haber un admin"
            if (targetUser.Rol.Nombre == "Admin")
            {
                // Contamos cuántos admins hay en total
                int totalAdmins = await _context.Users
                    .CountAsync(u => u.Rol.Nombre == "Admin");

                if (totalAdmins <= 1)
                {
                    throw new Exception("No puedes eliminar al último Administrador del sistema. Debe quedar al menos uno.");
                }
            }

            // --- EJECUCIÓN ---
            try
            {
                _context.Users.Remove(targetUser);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                // ESTO ES IMPORTANTE:
                // Si el usuario ya hizo ventas o cortes, SQL no te dejará borrarlo (Integridad Referencial).
                // En ese caso, sugerimos desactivarlo.
                throw new Exception("No se puede eliminar este usuario porque tiene registros asociados (Ventas, Pedidos). Te sugerimos desactivarlo (IsActive = false) en lugar de borrarlo.");
            }
        }
    }

}