using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IUserService
    {
        //Para Insertar Usuarios
        Task<User> RegisterAsync(RegisterUserDto datos, int requestorId);

        // Para Trae la informacion del perfil
        Task<UserProfileDto> GetUserProfileAsync(int userId);

        //Actualizacion de informacion de la cuenta
        Task<User> UpdateUserAsync(int id, UpdateUserDto datos, int requestorId);

        // Cmabiar contrasena 
        Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto datos);

        // Método para que un admin resetee la passwd de un usuario
        Task<bool> ResetPasswordByAdminAsync(int targetUserId, string newPassword);

        // 1. Traer todos
        Task<List<UserDto>> GetAllUsersAsync(int requestorId);

        // 2. Buscar por coincidencia (Nombre O Username)
        Task<List<UserDto>> SearchUsersAsync(string termino, int requestorId);

        //Eliminar 
        Task<bool> DeleteUserAsync(int targetUserId, int requestorId);
    }
}