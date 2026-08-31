using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api_Tlapaleria.Models
{
    [Table("PresentationPriceHistories")]
    public class PresentationPriceHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PresentationId { get; set; }
        [ForeignKey("PresentationId")]
        public ProductPresentation? Presentation { get; set; }

        // El ID desnormalizado que sugirió Claude para búsquedas rápidas
        [Required]
        public int ProductId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal OldPrice { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal NewPrice { get; set; }

        [Required]
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}