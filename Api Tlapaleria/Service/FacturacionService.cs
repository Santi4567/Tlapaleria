using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Api_Tlapaleria.Services
{
    public class FacturacionService : IFacturacionService
    {
        private readonly TlapaleriaContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly IBusinessConfigService _configService;
        private const string FacturapiBaseUrl = "https://www.facturapi.io/v2/";

        public FacturacionService(
            TlapaleriaContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            IBusinessConfigService configService)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _config = config;
            _configService = configService;
        }

        private async Task<HttpClient> CrearClienteHttpAsync()
        {
            var apiKey = await _configService.GetFacturapiApiKeyAsync();
            var client = _httpClientFactory.CreateClient("Facturapi");
            client.BaseAddress = new Uri(FacturapiBaseUrl);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            return client;
        }

        public async Task<FacturaResponseDto> EmitirFacturaVentaAsync(EmitirFacturaDto dto)
        {
            // Validar que el módulo de facturación esté activo
            var config = await _configService.GetConfigAsync();
            if (!config.FacturacionActiva)
            {
                throw new InvalidOperationException("El módulo de facturación electrónica no está habilitado en este sistema.");
            }

            // 1. Obtener la venta con sus detalles
            var sale = await _context.Sales
                .Include(s => s.Details)
                .FirstOrDefaultAsync(s => s.Id == dto.SaleId);

            if (sale == null)
            {
                throw new KeyNotFoundException($"No se encontró la venta con ID {dto.SaleId}.");
            }

            if (!sale.IsActive)
            {
                throw new InvalidOperationException("No se puede facturar una venta cancelada o inactiva.");
            }

            // 2. Validar que no tenga ya una factura activa
            var facturaExistente = await _context.Invoices
                .FirstOrDefaultAsync(i => i.SaleId == dto.SaleId && i.Status == "valid");

            if (facturaExistente != null)
            {
                throw new InvalidOperationException($"Esta venta ya fue facturada anteriormente con Folio Fiscal (UUID): {facturaExistente.Uuid}");
            }

            // 3. Determinar la forma de pago SAT
            string formaPago = !string.IsNullOrWhiteSpace(dto.FormaPago)
                ? dto.FormaPago
                : MapearFormaPagoSat(sale.PaymentMethod);

            // Código postal fiscal (si no se especifica, toma el de la ferretería)
            string codigoPostalFiscal = !string.IsNullOrWhiteSpace(dto.CodigoPostal)
                ? dto.CodigoPostal.Trim()
                : config.PostalCode;

            // 4. Mapear los productos al formato de Facturapi
            var itemsFacturapi = new List<Dictionary<string, object>>();

            foreach (var d in sale.Details.Where(d => d.IsActive))
            {
                var item = new Dictionary<string, object>
                {
                    ["quantity"] = d.Quantity,
                    ["product"] = new Dictionary<string, object>
                    {
                        ["description"] = string.IsNullOrWhiteSpace(d.ProductName) ? "Artículo de tlapalería" : d.ProductName,
                        ["product_key"] = "01010101", // Clave genérica SAT si no hay específica
                        ["price"] = d.UnitPrice,
                        ["tax_included"] = true,      // Desglosa el 16% de IVA automáticamente del precio final
                        ["taxes"] = new object[]
                        {
                            new
                            {
                                type = "IVA",
                                rate = 0.16m
                            }
                        },
                        ["unit_key"] = "H87"          // Clave SAT para "Pieza"
                    }
                };

                // Facturapi solo acepta 'discount' si tiene un monto positivo (no acepta null ni 0)
                if (d.DiscountAmount > 0)
                {
                    item["discount"] = d.DiscountAmount;
                }

                itemsFacturapi.Add(item);
            }

            if (!itemsFacturapi.Any())
            {
                throw new InvalidOperationException("La venta no tiene productos activos válidos para facturar.");
            }

            // 5. Estructurar el cliente (Receptor)
            var customerDict = new Dictionary<string, object>
            {
                ["legal_name"] = dto.RazonSocial.Trim().ToUpper(),
                ["tax_id"] = dto.Rfc.Trim().ToUpper(),
                ["tax_system"] = dto.RegimenFiscal.Trim(),
                ["address"] = new Dictionary<string, object>
                {
                    ["zip"] = codigoPostalFiscal
                }
            };

            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                customerDict["email"] = dto.Email.Trim();
            }

            // Estructurar el payload final para Facturapi v2 (CFDI 4.0)
            var invoicePayload = new Dictionary<string, object>
            {
                ["customer"] = customerDict,
                ["items"] = itemsFacturapi,
                ["payment_form"] = formaPago,
                ["payment_method"] = "PUE", // Pago en una sola exhibición para ventas directas
                ["use"] = dto.UsoCfdi.Trim().ToUpper()
            };

            // 6. Enviar petición a Facturapi
            var client = await CrearClienteHttpAsync();
            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(invoicePayload, jsonOptions),
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.PostAsync("invoices", jsonContent);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                string mensajeError = ExtraerMensajeError(responseBody);
                throw new InvalidOperationException($"Error al timbrar con el SAT: {mensajeError}");
            }

            // 7. Parsear la respuesta exitosa
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            string facturapiId = root.GetProperty("id").GetString() ?? string.Empty;
            string uuid = root.TryGetProperty("uuid", out var uuidProp) ? (uuidProp.GetString() ?? string.Empty) : string.Empty;
            decimal totalFactura = root.TryGetProperty("total", out var totalProp) ? totalProp.GetDecimal() : sale.TotalAmount;
            string status = root.TryGetProperty("status", out var statusProp) ? (statusProp.GetString() ?? "valid") : "valid";
            string? verificationUrl = root.TryGetProperty("verification_url", out var verProp) ? verProp.GetString() : null;

            // 8. Guardar la factura en la base de datos
            var nuevaFactura = new Invoice
            {
                SaleId = sale.Id,
                FacturapiId = facturapiId,
                Uuid = uuid,
                Rfc = dto.Rfc.Trim().ToUpper(),
                RazonSocial = dto.RazonSocial.Trim().ToUpper(),
                RegimenFiscal = dto.RegimenFiscal.Trim(),
                CodigoPostal = dto.CodigoPostal.Trim(),
                UsoCfdi = dto.UsoCfdi.Trim().ToUpper(),
                Total = totalFactura,
                Status = status,
                VerificationUrl = verificationUrl,
                CreatedAt = DateTime.Now
            };

            _context.Invoices.Add(nuevaFactura);
            await _context.SaveChangesAsync();

            // 9. Si se proporcionó un email, enviar automáticamente
            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await EnviarPorEmailAsync(facturapiId, dto.Email);
                    }
                    catch
                    {
                        // Si falla el envío de correo, no revertimos el timbrado
                    }
                });
            }

            return new FacturaResponseDto
            {
                Id = nuevaFactura.Id,
                SaleId = sale.Id,
                FolioVenta = sale.Folio,
                FacturapiId = facturapiId,
                Uuid = uuid,
                Rfc = nuevaFactura.Rfc,
                RazonSocial = nuevaFactura.RazonSocial,
                Total = nuevaFactura.Total,
                Status = nuevaFactura.Status,
                VerificationUrl = nuevaFactura.VerificationUrl,
                CreatedAt = nuevaFactura.CreatedAt
            };
        }

        public async Task<Invoice?> GetFacturaBySaleIdAsync(int saleId)
        {
            return await _context.Invoices
                .Include(i => i.Sale)
                .FirstOrDefaultAsync(i => i.SaleId == saleId);
        }

        public async Task<Invoice?> GetFacturaByIdAsync(int id)
        {
            return await _context.Invoices
                .Include(i => i.Sale)
                .FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<byte[]> DescargarPdfAsync(string facturapiId)
        {
            var client = await CrearClienteHttpAsync();
            var response = await client.GetAsync($"invoices/{facturapiId}/pdf");

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("No se pudo obtener el PDF de la factura.");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<byte[]> DescargarXmlAsync(string facturapiId)
        {
            var client = await CrearClienteHttpAsync();
            var response = await client.GetAsync($"invoices/{facturapiId}/xml");

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("No se pudo obtener el XML de la factura.");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<bool> EnviarPorEmailAsync(string facturapiId, string email)
        {
            var client = await CrearClienteHttpAsync();
            var payload = new { email = email.Trim() };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"invoices/{facturapiId}/email", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> CancelarFacturaAsync(int invoiceId, string motivo)
        {
            var factura = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (factura == null)
            {
                throw new KeyNotFoundException("No se encontró la factura a cancelar.");
            }

            if (factura.Status == "canceled")
            {
                throw new InvalidOperationException("Esta factura ya se encuentra cancelada.");
            }

            var client = await CrearClienteHttpAsync();
            // Facturapi cancela mediante DELETE /invoices/{id}?motive=02
            var response = await client.DeleteAsync($"invoices/{factura.FacturapiId}?motive={motivo}");

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Error al cancelar en el SAT: {ExtraerMensajeError(errorBody)}");
            }

            factura.Status = "canceled";
            factura.CancelReason = motivo;
            factura.CanceledAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        private static string MapearFormaPagoSat(string paymentMethod)
        {
            return paymentMethod?.Trim().ToLowerInvariant() switch
            {
                "efectivo" => "01",
                "transferencia" => "03",
                "tarjeta" => "04", // Tarjeta de crédito (o 28 si es débito)
                _ => "01"
            };
        }

        private static string ExtraerMensajeError(string responseBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (root.TryGetProperty("message", out var msg))
                {
                    string texto = msg.GetString() ?? responseBody;
                    if (root.TryGetProperty("details", out var det))
                    {
                        texto += $" ({det.GetString()})";
                    }
                    return texto;
                }
            }
            catch
            {
                // Si no es JSON estándar
            }

            return responseBody;
        }
    }
}
