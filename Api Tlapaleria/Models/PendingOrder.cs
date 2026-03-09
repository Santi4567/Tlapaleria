using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api_Tlapaleria.Models
{
    [Table("PendingOrders")]
    public class PendingOrder
    {
        [Key]
        public int Id { get; set; }

        // --- RELACIÓN: PRODUCTO ---
        [Required]
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public Product? Product { get; set; } // De aquí el Nombre y el Código

        // --- RELACIÓN: PROVEEDOR ---
        public int? SupplierId { get; set; } // Es 'int?' porque puede estar nulo al principio
        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; } // De aquí sacaremos a quién se le pide

        // --- RELACIÓN: USUARIO ---
        [Required]
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User? User { get; set; } // De aquí sacaremos quién lo pidió

        // --- DATOS DEL PEDIDO ---
        [Required]
        [MaxLength(100)]
        public string QuantityText { get; set; } // "3 bolsas" "1 pieza" "una caja etc"

        public string? Notes { get; set; } // "Si está caro, no pedir"

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Pendiente"; // Control de flujo

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}