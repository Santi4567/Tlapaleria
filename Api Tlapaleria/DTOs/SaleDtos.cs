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
        [RegularExpression("^(Efectivo|Tarjeta|Transferencia|Credito)$", ErrorMessage = "El método de pago debe ser Efectivo, Tarjeta, Transferencia o Credito.")]
        public string PaymentMethod { get; set; } = string.Empty;

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

        // --- CAMPOS PARA VENTA A CRÉDITO (O ASIGNACIÓN DE CLIENTE) ---
        public int? CustomerId { get; set; }

        // Anticipo / pago parcial opcional al momento de llevarse la mercancía a crédito
        [Range(0, 9999999.99, ErrorMessage = "El anticipo no puede ser negativo.")]
        public decimal? InitialPayment { get; set; }

        [RegularExpression("^(Efectivo|Tarjeta|Transferencia)$", ErrorMessage = "El método de pago del anticipo debe ser Efectivo, Tarjeta o Transferencia.")]
        public string? InitialPaymentMethod { get; set; }

        // Fecha compromiso de pago opcional para la cuenta de crédito
        public DateTime? DueDate { get; set; }

        [MaxLength(255, ErrorMessage = "La nota de crédito no puede exceder los 255 caracteres.")]
        public string? CreditNotes { get; set; }
    }
}
