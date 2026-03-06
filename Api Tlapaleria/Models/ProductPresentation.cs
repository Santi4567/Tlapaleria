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
        [JsonIgnore]
        [ForeignKey("ProductId")]
        public Product? Product { get; set; } // <--- Agrega el ?

        [Required]
        [MaxLength(50)]
        public string Name { get; set; }

        public string? Code { get; set; }    // <--- Agrega el ?
        public string? Barcode { get; set; } // <--- Agrega el ?

        public decimal Price { get; set; }
        public decimal StockFactor { get; set; }

        public bool IsActive { get; set; } = true;
    }
}