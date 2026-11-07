using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class EmitirFacturaDto
    {
        [Required(ErrorMessage = "El ID de la venta es obligatorio.")]
        public int SaleId { get; set; }

        [Required(ErrorMessage = "El RFC es obligatorio.")]
        [RegularExpression(@"^[A-Z&Ñ]{3,4}\d{6}[A-Z\d]{3}$", ErrorMessage = "El formato de RFC no es válido.")]
        public string Rfc { get; set; } = string.Empty;

        [Required(ErrorMessage = "La razón social o nombre completo es obligatorio.")]
        [MaxLength(250)]
        public string RazonSocial { get; set; } = string.Empty;

        [Required(ErrorMessage = "El régimen fiscal es obligatorio.")]
        [RegularExpression(@"^\d{3}$", ErrorMessage = "El régimen fiscal debe tener 3 dígitos (ej: 601, 612, 616, 626).")]
        public string RegimenFiscal { get; set; } = string.Empty;

        [Required(ErrorMessage = "El código postal fiscal es obligatorio.")]
        [RegularExpression(@"^\d{5}$", ErrorMessage = "El código postal debe tener 5 dígitos.")]
        public string CodigoPostal { get; set; } = string.Empty;

        [Required(ErrorMessage = "El uso del CFDI es obligatorio.")]
        [MaxLength(10)]
        public string UsoCfdi { get; set; } = "G01"; // G01: Adquisición de mercancías, G03: Gastos en general, etc.

        // Opcional: Clave SAT ("01" Efectivo, "03" Transferencia, "04" TDC, "28" TDD). Si es null se toma de la venta.
        public string? FormaPago { get; set; }

        [EmailAddress(ErrorMessage = "El correo electrónico no tiene un formato válido.")]
        public string? Email { get; set; }
    }

    public class CancelarFacturaDto
    {
        [Required(ErrorMessage = "El motivo de cancelación es obligatorio.")]
        [RegularExpression("^(01|02|03|04)$", ErrorMessage = "El motivo debe ser '01', '02', '03' o '04'.")]
        public string Motivo { get; set; } = "02"; // 02: Comprobante emitido con errores sin relación
    }

    public class EnviarEmailFacturaDto
    {
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Correo electrónico no válido.")]
        public string Email { get; set; } = string.Empty;
    }

    public class FacturaResponseDto
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public string FolioVenta { get; set; } = string.Empty;
        public string FacturapiId { get; set; } = string.Empty;
        public string Uuid { get; set; } = string.Empty;
        public string Rfc { get; set; } = string.Empty;
        public string RazonSocial { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? VerificationUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
