using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class BusinessConfigDto
    {
        public string BusinessName { get; set; } = string.Empty;
        public string? Rfc { get; set; }
        public string PostalCode { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool FacturacionActiva { get; set; }
        public string? FacturapiApiKey { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UpdateBusinessConfigDto
    {
        [Required(ErrorMessage = "El nombre del negocio es obligatorio.")]
        [MaxLength(150)]
        public string BusinessName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Rfc { get; set; }

        [Required(ErrorMessage = "El código postal es obligatorio.")]
        [RegularExpression(@"^\d{5}$", ErrorMessage = "El código postal debe tener 5 dígitos.")]
        public string PostalCode { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
        public string? Email { get; set; }

        public bool FacturacionActiva { get; set; } = true;

        [MaxLength(200)]
        public string? FacturapiApiKey { get; set; }
    }
}
