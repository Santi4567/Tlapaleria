using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Api_Tlapaleria.Enums;

namespace Api_Tlapaleria.Models
{
    [Table("PendingOrders")]
    public class PendingOrder
    {
        [Key]
        public int Id { get; set; }

        // --- RELACIÓN: PRODUCTO ---
        // Se quitó [Required] y se agregó '?' a int para que sea opcional
        public int? ProductId { get; set; }

        // Se agregó la columna para el texto de los productos nuevos
        [MaxLength(150)]
        public string? NewProductName { get; set; }

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
        public PendingOrderStatus Status { get; set; } = PendingOrderStatus.Pendiente; // Control de flujo

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // --- RELACIÓN: HISTORIAL ---
        // Esto permite a Entity Framework saber que un pedido tiene muchos movimientos
        public virtual ICollection<PendingOrderHistory> History { get; set; } = new List<PendingOrderHistory>();
    }
}