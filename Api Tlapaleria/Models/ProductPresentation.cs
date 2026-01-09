using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Api_Tlapaleria.Models
{
    [Table("ProductPresentations")]
    public class ProductPresentation
    {
        [Key]
        public int Id { get; set; }

        public int ProductId { get; set; }
        [JsonIgnore] // Para evitar ciclos infinitos
        [ForeignKey("ProductId")]
        public Product Product { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } // "Medio Kilo", "Pieza"

        public string Code { get; set; }
        public string Barcode { get; set; }

        public decimal Price { get; set; } // Precio Público (ej: 25.00)

        // Cuánto descuenta del inventario padre
        // Ej: Kilo = 1.0, Medio = 0.5, Cuarto = 0.25
        public decimal StockFactor { get; set; }

        public bool IsActive { get; set; } = true;
    }
}