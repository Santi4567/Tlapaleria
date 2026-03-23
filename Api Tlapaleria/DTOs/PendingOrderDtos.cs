using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class CreatePendingOrderDto
    {
        [Required(ErrorMessage = "Debes seleccionar un producto.")]
        public int ProductId { get; set; }

        public int? SupplierId { get; set; }

        // EL UserId se obtiene del Token de session 

        [Required(ErrorMessage = "Debes especificar la cantidad (ej: '3 cajas' o '10 kg').")]
        [MaxLength(100)]
        public string QuantityText { get; set; }

        public string? Notes { get; set; }
    }
    public class UpdatePendingOrderDto
    {
        public int? SupplierId { get; set; }

        [Required(ErrorMessage = "Debes especificar la cantidad (ej: '3 cajas' o '10 kg').")]
        [MaxLength(100)]
        public string QuantityText { get; set; }

        public string? Notes { get; set; }
    }
}