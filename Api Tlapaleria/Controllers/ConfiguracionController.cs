using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace Api_Tlapaleria.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ConfiguracionController : ControllerBase
    {
        private readonly IBusinessConfigService _configService;
        private readonly IHttpClientFactory _httpClientFactory;

        public ConfiguracionController(
            IBusinessConfigService configService,
            IHttpClientFactory httpClientFactory)
        {
            _configService = configService;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<BusinessConfigDto>>> GetConfig()
        {
            try
            {
                var config = await _configService.GetConfigAsync();
                var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
                bool esAdmin = string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase);

                // Si no es Administrador, se oculta la API Key de Facturapi para protegerla
                if (!esAdmin && !string.IsNullOrEmpty(config.FacturapiApiKey))
                {
                    config.FacturapiApiKey = "••••••••••••••••";
                }

                return Ok(ApiResponse<BusinessConfigDto>.Exito(config));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<BusinessConfigDto>.Error($"Error al obtener la configuración: {ex.Message}"));
            }
        }

        [HttpPut]
        public async Task<ActionResult<ApiResponse<BusinessConfigDto>>> UpdateConfig([FromBody] UpdateBusinessConfigDto dto)
        {
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
            if (!string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<BusinessConfigDto>.Error("Acceso denegado. Solo los administradores pueden modificar la configuración del negocio."));
            }

            try
            {
                var updated = await _configService.UpdateConfigAsync(dto);
                return Ok(ApiResponse<BusinessConfigDto>.Exito(updated, "Configuración actualizada con éxito."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<BusinessConfigDto>.Error($"Error al guardar la configuración: {ex.Message}"));
            }
        }

        [HttpPost("probar-facturapi")]
        public async Task<ActionResult<ApiResponse<bool>>> ProbarFacturapi([FromBody] ProbarFacturapiDto dto)
        {
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
            if (!string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<bool>.Error("Acceso denegado. Solo los administradores pueden probar la conexión de Facturapi."));
            }

            try
            {
                string keyToTest = !string.IsNullOrWhiteSpace(dto.ApiKey)
                    ? dto.ApiKey.Trim()
                    : await _configService.GetFacturapiApiKeyAsync();

                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri("https://www.facturapi.io/v2/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", keyToTest);

                var response = await client.GetAsync("invoices?limit=1");
                if (response.IsSuccessStatusCode)
                {
                    return Ok(ApiResponse<bool>.Exito(true, "¡Conexión exitosa con Facturapi!"));
                }

                var errorBody = await response.Content.ReadAsStringAsync();
                return BadRequest(ApiResponse<bool>.Error($"Facturapi rechazó la llave: {errorBody}"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.Error($"Error al verificar la conexión: {ex.Message}"));
            }
        }
    }

    public class ProbarFacturapiDto
    {
        public string? ApiKey { get; set; }
    }
}
