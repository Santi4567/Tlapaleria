using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class BusinessConfigService : IBusinessConfigService
    {
        private readonly TlapaleriaContext _context;
        private readonly IConfiguration _config;

        public BusinessConfigService(TlapaleriaContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        private async Task<BusinessConfig> GetOrCreateConfigEntityAsync()
        {
            var config = await _context.BusinessConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                // Si la tabla está vacía, sembramos la fila por defecto
                config = new BusinessConfig
                {
                    BusinessName = "Tlapalería y Materiales",
                    PostalCode = "72310",
                    FacturacionActiva = true,
                    FacturapiApiKey = _config["Facturapi:ApiKey"],
                    UpdatedAt = DateTime.Now
                };

                _context.BusinessConfigs.Add(config);
                await _context.SaveChangesAsync();
            }
            return config;
        }

        public async Task<BusinessConfigDto> GetConfigAsync()
        {
            var entity = await GetOrCreateConfigEntityAsync();
            return new BusinessConfigDto
            {
                BusinessName = entity.BusinessName,
                Rfc = entity.Rfc,
                PostalCode = entity.PostalCode,
                Address = entity.Address,
                Phone = entity.Phone,
                Email = entity.Email,
                FacturacionActiva = entity.FacturacionActiva,
                FacturapiApiKey = entity.FacturapiApiKey,
                UpdatedAt = entity.UpdatedAt
            };
        }

        public async Task<BusinessConfigDto> UpdateConfigAsync(UpdateBusinessConfigDto dto)
        {
            var entity = await GetOrCreateConfigEntityAsync();

            entity.BusinessName = dto.BusinessName.Trim();
            entity.Rfc = string.IsNullOrWhiteSpace(dto.Rfc) ? null : dto.Rfc.Trim().ToUpper();
            entity.PostalCode = dto.PostalCode.Trim();
            entity.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
            entity.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            entity.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            entity.FacturacionActiva = dto.FacturacionActiva;

            if (!string.IsNullOrWhiteSpace(dto.FacturapiApiKey))
            {
                entity.FacturapiApiKey = dto.FacturapiApiKey.Trim();
            }

            entity.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return await GetConfigAsync();
        }

        public async Task<string> GetFacturapiApiKeyAsync()
        {
            var entity = await GetOrCreateConfigEntityAsync();
            if (!string.IsNullOrWhiteSpace(entity.FacturapiApiKey) && 
                !entity.FacturapiApiKey.Contains("TU_LLAVE", StringComparison.OrdinalIgnoreCase))
            {
                return entity.FacturapiApiKey.Trim();
            }

            var appSettingsKey = _config["Facturapi:ApiKey"];
            if (!string.IsNullOrWhiteSpace(appSettingsKey) && 
                !appSettingsKey.Contains("TU_LLAVE", StringComparison.OrdinalIgnoreCase))
            {
                return appSettingsKey.Trim();
            }

            throw new InvalidOperationException("La API Key de Facturapi no ha sido configurada en el sistema. Configúrala en la pantalla de Configuración.");
        }

        public async Task<string> GetPostalCodeAsync()
        {
            var entity = await GetOrCreateConfigEntityAsync();
            return string.IsNullOrWhiteSpace(entity.PostalCode) ? "72310" : entity.PostalCode.Trim();
        }
    }
}
