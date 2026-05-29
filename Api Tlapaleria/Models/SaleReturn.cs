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

        [MaxLength(255)]
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Propiedad de navegación: Lista de artículos devueltos en esta operación
        public List<ReturnDetail> Details { get; set; } = new List<ReturnDetail>();
    }
}