using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class CreateSaleDetailDto
    {
        // PEDIMOS LA PRESENTACIÓN, NO EL PRODUCTO BASE
        [Required(ErrorMessage = "La presentación del producto es obligatoria.")]
        public int PresentationId { get; set; }

        [Range(0.001, 999999.999, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public decimal Quantity { get; set; }
    }

    public class CreateSaleDto
    {
        [Required]
        [RegularExpression("^(Efectivo|Tarjeta|Transferencia)$")]
        public string PaymentMethod { get; set; }

        [MaxLength(100, ErrorMessage = "La referencia no puede exceder los 100 caracteres.")]
        public string? PaymentReference { get; set; }

        [Required]
        [MinLength(1)]
        public List<CreateSaleDetailDto> Details { get; set; } = new List<CreateSaleDetailDto>();

        public string? ConnectionId { get; set; }

        // DESCUENTO OPCIONAL (requiere permiso 'apply.discounts'). Usar solo uno de los dos.
        [Range(0, 100, ErrorMessage = "El porcentaje de descuento debe estar entre 0 y 100.")]
        public decimal? DiscountPercent { get; set; }

        [Range(0, 9999999.99, ErrorMessage = "El monto de descuento no puede ser negativo.")]
        public decimal? DiscountAmount { get; set; }
    }
}