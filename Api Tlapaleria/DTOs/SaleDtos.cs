using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    // 1. El DTO para cada renglón del carrito
    public class CreateSaleDetailDto
    {
        [Required(ErrorMessage = "El ID del producto es obligatorio.")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "La cantidad es obligatoria.")]
        [Range(0.001, double.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public decimal Quantity { get; set; }
    }

    // 2. El DTO Maestro (La envoltura del ticket)
    public class CreateSaleDto
    {
        [Required(ErrorMessage = "El método de pago es obligatorio.")]
        [RegularExpression("^(Efectivo|Tarjeta|Transferencia)$",
            ErrorMessage = "Método de pago no válido. Usa: Efectivo, Tarjeta o Transferencia.")]
        public string PaymentMethod { get; set; }

        // Validamos que no nos manden un ticket vacío
        [Required(ErrorMessage = "El carrito no puede estar vacío.")]
        [MinLength(1, ErrorMessage = "Debe haber al menos un producto en la venta.")]
        public List<CreateSaleDetailDto> Details { get; set; } = new List<CreateSaleDetailDto>();
    }
}