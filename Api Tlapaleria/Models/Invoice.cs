using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Api_Tlapaleria.Models
{
    [Table("Invoices")]
    public class Invoice
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SaleId { get; set; }

        [ForeignKey("SaleId")]
        public Sale? Sale { get; set; }

        [Required]
        [MaxLength(50)]
        public string FacturapiId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Uuid { get; set; } = string.Empty; // Folio fiscal digital del SAT

        [Required]
        [MaxLength(20)]
        public string Rfc { get; set; } = string.Empty;

        [Required]
        [MaxLength(250)]
        public string RazonSocial { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string RegimenFiscal { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string CodigoPostal { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string UsoCfdi { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Total { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "valid"; // "valid" o "canceled"

        [MaxLength(500)]
        public string? VerificationUrl { get; set; }

        [MaxLength(50)]
        public string? CancelReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? CanceledAt { get; set; }
    }
}
