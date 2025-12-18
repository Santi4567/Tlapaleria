using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IUserService
    {
        //Para Insertar Usuarios
        Task<User> RegisterAsync(RegisterUserDto datos);

        // Para Trae la informacion del perfil
        Task<UserProfileDto> GetUserProfileAsync(int userId);

        //Actualizacion de informacion de la cuenta
        Task<User> UpdateUserAsync(int id, UpdateUserDto datos);

        // Cmabiar contrasena 
        Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto datos);

        // Método para que un jefe resetee la pass de un empleado
        Task<bool> ResetPasswordByAdminAsync(int targetUserId, string newPassword);
    }
}