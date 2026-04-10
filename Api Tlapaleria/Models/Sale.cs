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
        [MaxLength(20)]
        public string Folio { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Required(ErrorMessage = "El método de pago es obligatorio.")]
        [MaxLength(50)]
        public string PaymentMethod { get; set; } = string.Empty;

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