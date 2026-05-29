using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Api_Tlapaleria.Models
{
    [Table("SaleDetails")]
    public class SaleDetail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SaleId { get; set; }
        [JsonIgnore]
        [ForeignKey("SaleId")]
        public Sale? Sale { get; set; }

        [Required]
        public int ProductId { get; set; }
        [JsonIgnore]
        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [Required]
        public int PresentationId { get; set; }
        [JsonIgnore]
        [ForeignKey("PresentationId")]
        public ProductPresentation? Presentation { get; set; }

        // --- LA LIBRETA CON LAPICERO (Snapshot) ---
        [Required]
        [MaxLength(150)]
        public string ProductName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Brand { get; set; }

        [Required]
        public decimal Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,3)")]
        public decimal StockFactorApplied { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Subtotal { get; set; }

        // --- COLUMNAS PARA DEVOLUCIONES PARCIALES ---

        [Required]
        public bool IsActive { get; set; } = true; // 1 = Activo/Vendido, 0 = Devuelto

        public DateTime UpdatedAt { get; set; } = DateTime.Now; // Control de fecha de la devolución
    }
}