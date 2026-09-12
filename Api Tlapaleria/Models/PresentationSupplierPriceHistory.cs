using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api_Tlapaleria.Models
{
    [Table("PresentationSupplierPriceHistories")]
    public class PresentationSupplierPriceHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PresentationId { get; set; }
        [ForeignKey("PresentationId")]
        public ProductPresentation? Presentation { get; set; }

        [Required]
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal OldSupplierPrice { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal NewSupplierPrice { get; set; }

        [Required]
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}