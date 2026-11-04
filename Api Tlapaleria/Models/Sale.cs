using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api_Tlapaleria.Models
{
    [Table("Sales")]
    public class Sale
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El folio es obligatorio.")]
        [MaxLength(50)]
        public string Folio { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; } // Total NETO cobrado al cliente (ya con descuento aplicado)

        // --- DESCUENTO (clientes/familiares) ---
        // Porcentaje aplicado (0 si el descuento fue por monto fijo o no hubo descuento)
        [Column(TypeName = "decimal(5,2)")]
        public decimal DiscountPercent { get; set; } = 0;

        // Monto total descontado en pesos (TotalAmount + DiscountAmount = suma de precios de lista)
        [Column(TypeName = "decimal(10,2)")]
        public decimal DiscountAmount { get; set; } = 0;

        // Archivo: Sale.cs
        [Required(ErrorMessage = "El método de pago es obligatorio.")]
        [MaxLength(50)]
        public string PaymentMethod { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? PaymentReference { get; set; } // Puede ser "500", "AUTH-048291", "SPEI-884920", etc.

        // Relación con el Cajero
        [Required]
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User? User { get; set; }

        // El estado booleano que agregamos para el corte de caja
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Propiedad de navegación: Lista de productos en este ticket
        public List<SaleDetail> Details { get; set; } = new List<SaleDetail>();
    }
}