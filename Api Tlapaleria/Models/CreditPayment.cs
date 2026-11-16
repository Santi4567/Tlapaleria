using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Api_Tlapaleria.Models
{
    [Table("CreditPayments")]
    public class CreditPayment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string PaymentFolio { get; set; } = string.Empty; // Ej. ABN-261116120000-1234

        [Required]
        public int CreditAccountId { get; set; }

        [JsonIgnore]
        [ForeignKey("CreditAccountId")]
        public CreditAccount? CreditAccount { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [JsonIgnore]
        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        // Cajero que recibió el abono
        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        // Efectivo | Tarjeta | Transferencia
        [Required]
        [MaxLength(50)]
        public string PaymentMethod { get; set; } = "Efectivo";

        [MaxLength(100)]
        public string? PaymentReference { get; set; }

        [MaxLength(255)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
