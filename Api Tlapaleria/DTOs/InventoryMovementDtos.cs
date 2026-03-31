using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class CreateInventoryMovementDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [RegularExpression("^(Entrada|Merma|Ajuste Positivo|Ajuste Negativo)$",
            ErrorMessage = "Tipo de movimiento inválido.")]
        public string MovementType { get; set; }

        [Required]
        [Range(0.001, double.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public decimal Quantity { get; set; }

        public string? Notes { get; set; }
    }
}