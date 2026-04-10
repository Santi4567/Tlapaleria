using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class CreateSaleDetailDto
    {
        // PEDIMOS LA PRESENTACIÓN, NO EL PRODUCTO BASE
        [Required(ErrorMessage = "La presentación del producto es obligatoria.")]
        public int PresentationId { get; set; }

        [Required(ErrorMessage = "La cantidad es obligatoria.")]
        [Range(0.001, double.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public decimal Quantity { get; set; }
    }

    public class CreateSaleDto
    {
        [Required]
        [RegularExpression("^(Efectivo|Tarjeta|Transferencia)$")]
        public string PaymentMethod { get; set; }

        [Required]
        [MinLength(1)]
        public List<CreateSaleDetailDto> Details { get; set; } = new List<CreateSaleDetailDto>();
    }
}