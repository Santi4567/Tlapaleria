using System.ComponentModel.DataAnnotations;
using Api_Tlapaleria.Enums;

namespace Api_Tlapaleria.DTOs
{
    public class CreatePendingOrderDto
    {
        // Ahora es opcional (permite NULL)
        public int? ProductId { get; set; }

        // Campo para productos fuera de catálogo
        [MaxLength(150)]
        public string? NewProductName { get; set; }

        public int? SupplierId { get; set; }

        // EL UserId se obtiene del Token de session 

        [Required(ErrorMessage = "Debes especificar la cantidad (ej: '3 cajas' o '10 kg').")]
        [MaxLength(100)]
        public string QuantityText { get; set; }

        public string? Notes { get; set; }
    }
    public class UpdatePendingOrderDto
    {
        public int? ProductId { get; set; } // <-- Para enlazar productos temporales

        public int? SupplierId { get; set; }

        [Required(ErrorMessage = "Debes especificar la cantidad (ej: '3 cajas' o '10 kg').")]
        [MaxLength(100)]
        public string QuantityText { get; set; }

        public string? Notes { get; set; }
    }
    public class UpdatePendingOrderStatusDto
    {
        [Required(ErrorMessage = "El estado es obligatorio.")]
        public PendingOrderStatus Status { get; set; }
    }

    //Historial de cambio de estados 
    public class PendingOrderHistoryDto
    {
        public int Id { get; set; }
        public int PendingOrderId { get; set; }
        public string StatusName { get; set; }
        public string UserName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}