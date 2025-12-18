using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IUserService
    {
        Task<User> RegisterAsync(RegisterUserDto datos);
    }
}