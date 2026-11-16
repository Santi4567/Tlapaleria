using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Api_Tlapaleria.Models
{
    [Table("Returns")]
    public class SaleReturn
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El folio de la devolución es obligatorio.")]
        [MaxLength(50)]
        public string ReturnFolio { get; set; } = string.Empty;

        // Relación con el Ticket Original
        [Required]
        public int SaleId { get; set; }

        [ForeignKey("SaleId")]
        public Sale? Sale { get; set; }

        // Relación con el Cajero que autoriza la devolución
        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalRefunded { get; set; }

        // --- DESGLOSE PARA VENTAS A CRÉDITO ---
        // Monto que se descontó directamente de la deuda pendiente del cliente (no salió efectivo de caja)
        [Column(TypeName = "decimal(10,2)")]
        public decimal AppliedToDebtAmount { get; set; } = 0;

        // Monto que efectivamente se reembolsó en dinero al cliente (100% en ventas de contado, o excedente si ya había abonado de más en crédito)
        [Column(TypeName = "decimal(10,2)")]
        public decimal CashRefundedAmount { get; set; } = 0;

        [MaxLength(255)]
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Propiedad de navegación: Lista de artículos devueltos en esta operación
        public List<ReturnDetail> Details { get; set; } = new List<ReturnDetail>();
    }
}