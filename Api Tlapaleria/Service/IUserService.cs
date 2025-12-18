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
    }
}