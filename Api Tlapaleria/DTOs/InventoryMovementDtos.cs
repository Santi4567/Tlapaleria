using System.ComponentModel.DataAnnotations;
using Api_Tlapaleria.Enums;

namespace Api_Tlapaleria.DTOs
{
    public class CreateInventoryMovementDto
    {
        [Required]
        public int ProductId { get; set; }

        // ASEGÚRATE DE QUE AQUÍ DIGA 'MovementType' EN VEZ DE 'string'
        [Required(ErrorMessage = "El tipo de movimiento es obligatorio.")]
        public MovementType MovementType { get; set; }

        [Required]
        [Range(0.001, double.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public decimal Quantity { get; set; }

        public string? Notes { get; set; }
    }
}