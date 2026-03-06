using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api_Tlapaleria.Models
{
    [Table("Products")]
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string InternalCode { get; set; }

        [MaxLength(100)]
        public string? Barcode { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        public string? Description { get; set; }
        public string? Brand { get; set; }

        [MaxLength(100)]
        public string? Location { get; set; }

        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal SupplierPrice { get; set; } // Costo

        // --- NUEVO CAMPO ---
        [Column(TypeName = "decimal(5,2)")]
        public decimal? ProfitMargin { get; set; } // Puede ser nulo
        // -------------------

        public DateTime? LastOrderDate { get; set; }

        [Required]
        [MaxLength(20)]
        public string UnitOfMeasure { get; set; }

        [Column(TypeName = "decimal(10,3)")]
        public decimal CurrentStock { get; set; }

        public List<ProductPresentation> Presentations { get; set; } = new List<ProductPresentation>();

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}