using Api_Tlapaleria.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Importante para [NotMapped]

namespace Api_Tlapaleria.Models
{
    [Table("InventoryMovements")]
    public class InventoryMovement
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [Required]
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User? User { get; set; }

        // 1. ESTO ES LO QUE SE GUARDA EN LA BASE DE DATOS (El número)
        [Required]
        public MovementType MovementType { get; set; }

        // 2. ESTO ES LO QUE SE AGREGA AL JSON MÁGICAMENTE (El texto)
        [NotMapped]
        public string MovementTypeName => MovementType.ToString();

        [Column(TypeName = "decimal(10,3)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(10,3)")]
        public decimal PreviousStock { get; set; }

        [Column(TypeName = "decimal(10,3)")]
        public decimal NewStock { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}