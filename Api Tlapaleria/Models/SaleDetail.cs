using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api_Tlapaleria.Models
{
    [Table("SaleDetails")]
    public class SaleDetail
    {
        [Key]
        public int Id { get; set; }

        // Relación con el Ticket Padre
        [Required]
        public int SaleId { get; set; }
        [ForeignKey("SaleId")]
        public Sale? Sale { get; set; }

        // Relación con el Catálogo (El "Ancla" para devoluciones)
        [Required]
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        // --- LOS DATOS CONGELADOS (Snapshot) ---

        [Required]
        [MaxLength(150)]
        public string ProductName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Brand { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,3)")] // Soporta 3 decimales para gramos/litros
        public decimal Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Subtotal { get; set; }
    }
}