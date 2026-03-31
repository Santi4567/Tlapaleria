using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        [Required]
        [MaxLength(30)]
        public string MovementType { get; set; }

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