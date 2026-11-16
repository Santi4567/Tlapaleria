using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Api_Tlapaleria.Enums;

namespace Api_Tlapaleria.Models
{
    [Table("CreditAccounts")]
    public class CreditAccount
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Folio { get; set; } = string.Empty; // Ej. CRD-261116103000-4821

        [Required]
        public int CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        [Required]
        public int SaleId { get; set; }

        [ForeignKey("SaleId")]
        public Sale? Sale { get; set; }

        // Cajero / Usuario que otorgó el crédito
        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        // Monto original del ticket cuando se llevó la mercancía (ya neto de descuentos si hubo)
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal OriginalAmount { get; set; }

        // Monto descontado de la deuda por devoluciones de mercancía de este ticket
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal ReturnedAmount { get; set; } = 0;

        // Suma de todos los abonos realizados a esta cuenta (incluyendo anticipo inicial si dejó)
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal PaidAmount { get; set; } = 0;

        // Lo que debe actualmente de esta cuenta: Math.Max(0, OriginalAmount - ReturnedAmount - PaidAmount)
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Balance { get; set; }

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = CreditAccountStatus.Pendiente.ToString();

        // Fecha compromiso de pago opcional
        public DateTime? DueDate { get; set; }

        [MaxLength(255)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public DateTime? PaidAt { get; set; }

        // Historial de abonos de esta cuenta
        public List<CreditPayment> Payments { get; set; } = new List<CreditPayment>();
    }
}
