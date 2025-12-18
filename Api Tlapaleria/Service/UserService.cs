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
        public async Task<User> RegisterAsync(RegisterUserDto datos)
        {
            // 1. Validar que el usuario no exista
            var existe = await _context.Users.AnyAsync(u => u.Username == datos.Username);
            if (existe)
            {
                throw new Exception($"El usuario '{datos.Username}' ya existe.");
            }

            // 2. BUSCAR EL ID DEL ROL (La parte clave de la relación)
            var rolEncontrado = await _context.Roles
                .FirstOrDefaultAsync(r => r.Nombre == datos.RolNombre);

            if (rolEncontrado == null)
            {
                throw new Exception($"El rol '{datos.RolNombre}' no es válido.");
            }

            // 3. Encriptar contraseña
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(datos.Password);

            // 4. Crear el objeto User con la relación
            var nuevoUsuario = new User
            {
                Username = datos.Username,
                Passwd = passwordHash,
                Name = datos.Name,
                RolId = rolEncontrado.Id, // Asignamos el ID que encontramos en la BD
                IsActive = false 
            };

            // 5. Guardar
            _context.Users.Add(nuevoUsuario);
            await _context.SaveChangesAsync();

            // 6. Cargar la relación para devolver el objeto bonito (con nombre de rol)
            // Esto hace que la respuesta lleve "rol": { "id": 2, "nombre": "Vendedor" }
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

        public async Task<User> UpdateUserAsync(int id, UpdateUserDto datos)
        {
            // 1. Buscar al usuario que queremos editar
            var user = await _context.Users.FindAsync(id);
            if (user == null) throw new Exception("Usuario no encontrado.");

            // 2. VALIDAR DUPLICADOS (Username)
            // "Busca si existe ALGUIEN MÁS (Id != id) que ya tenga ese username"
            bool nombreOcupado = await _context.Users
                .AnyAsync(u => u.Username == datos.Username && u.Id != id);

            if (nombreOcupado)
            {
                throw new Exception($"El nombre de usuario '{datos.Username}' ya está en uso por otra persona.");
            }

            // 3. VALIDAR Y BUSCAR EL ROL
            // Solo buscamos en la BD si el rol cambió para ahorrar recursos, 
            // pero para seguridad buscamos siempre el ID del nombre que nos mandan.
            var rolEncontrado = await _context.Roles
                .FirstOrDefaultAsync(r => r.Nombre == datos.RolNombre);

            if (rolEncontrado == null)
            {
                throw new Exception($"El rol '{datos.RolNombre}' no existe.");
            }

            // 4. ACTUALIZAR LOS DATOS (¡Sin tocar el Password!)
            user.Name = datos.Name;
            user.Username = datos.Username;
            user.RolId = rolEncontrado.Id; // Actualizamos la relación
            user.IsActive = datos.IsActive; // Aquí puedes activar/desactivar

            // 5. GUARDAR CAMBIOS
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // 6. Cargar la relación para devolver el objeto completo
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
        public async Task<List<UserDto>> GetAllUsersAsync()
        {
            // Usamos AsNoTracking porque solo vamos a leer, es más rápido
            var usuarios = await _context.Users
                .AsNoTracking()
                .Include(u => u.Rol) // Importante para sacar el nombre del rol
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Username = u.Username,
                    Rol = u.Rol.Nombre, // Accedemos a la navegación
                    IsActive = u.IsActive
                })
                .ToListAsync();

            return usuarios;
        }

        //Buscar Usuarios 
        public async Task<List<UserDto>> SearchUsersAsync(string termino)
        {
            if (string.IsNullOrWhiteSpace(termino))
                return new List<UserDto>();

            // Buscamos si el término está en el Nombre O (||) en el Username
            var usuarios = await _context.Users
                .AsNoTracking()
                .Include(u => u.Rol)
                .Where(u => u.Name.Contains(termino) || u.Username.Contains(termino))
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