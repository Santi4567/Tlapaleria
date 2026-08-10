using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Api_Tlapaleria.Enums;

namespace Api_Tlapaleria.Models
{
    [Table("PendingOrderHistory")] // <-- ESTA LÍNEA SOLUCIONA EL ERROR
    public class PendingOrderHistory
    {
        [Key]
        public int Id { get; set; }

        public int PendingOrderId { get; set; }

        public PendingOrderStatus Status { get; set; }

        public int UserId { get; set; }

        public DateTime CreatedAt { get; set; }

        // Relaciones
        [ForeignKey("PendingOrderId")]
        public virtual PendingOrder PendingOrder { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
    }
}