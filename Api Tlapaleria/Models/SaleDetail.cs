using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api_Tlapaleria.Models
{
    [Table("SaleDetails")]
    public class SaleDetail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SaleId { get; set; }
        [ForeignKey("SaleId")]
        public Sale? Sale { get; set; }

        // --- RELACIONES ---
        [Required]
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        // NUEVA RELACIÓN: Para saber exactamente en qué formato se vendió
        [Required]
        public int PresentationId { get; set; }
        [ForeignKey("PresentationId")]
        public ProductPresentation? Presentation { get; set; }

        // --- LA LIBRETA CON LAPICERO ---
        [Required]
        [MaxLength(150)]
        public string ProductName { get; set; } = string.Empty; // Ej: "Clavos 2 Pulgadas - Caja 100 pzas"

        [MaxLength(100)]
        public string? Brand { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,3)")]
        public decimal Quantity { get; set; } // Cuántas cajas se vendieron

        // Guardamos el multiplicador por si alguna vez cambia en el catálogo, 
        // saber cuánto stock le quitó este ticket a la base.
        [Required]
        [Column(TypeName = "decimal(10,3)")]
        public decimal StockFactorApplied { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; } // Precio de la caja

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Subtotal { get; set; }
    }
}