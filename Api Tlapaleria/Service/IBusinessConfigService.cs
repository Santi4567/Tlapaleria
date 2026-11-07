using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IBusinessConfigService
    {
        Task<BusinessConfigDto> GetConfigAsync();
        Task<BusinessConfigDto> UpdateConfigAsync(UpdateBusinessConfigDto dto);
        Task<string> GetFacturapiApiKeyAsync();
        Task<string> GetPostalCodeAsync();
    }
}
