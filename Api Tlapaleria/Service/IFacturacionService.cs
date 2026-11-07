using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IFacturacionService
    {
        Task<FacturaResponseDto> EmitirFacturaVentaAsync(EmitirFacturaDto dto);
        Task<Invoice?> GetFacturaBySaleIdAsync(int saleId);
        Task<Invoice?> GetFacturaByIdAsync(int id);
        Task<byte[]> DescargarPdfAsync(string facturapiId);
        Task<byte[]> DescargarXmlAsync(string facturapiId);
        Task<bool> EnviarPorEmailAsync(string facturapiId, string email);
        Task<bool> CancelarFacturaAsync(int invoiceId, string motivo);
    }
}
