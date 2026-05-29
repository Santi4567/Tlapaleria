using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class CreateReturnDetailDto
    {
        [Required(ErrorMessage = "El identificador del renglón original es obligatorio.")]
        public int SaleDetailId { get; set; }

        [Required(ErrorMessage = "La cantidad a devolver es obligatoria.")]
        // Acepta desde 0.001 (1 gramo/milímetro) hacia arriba
        [Range(0.001, double.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public decimal QuantityReturned { get; set; }
    }

    public class CreateReturnDto
    {
        [Required(ErrorMessage = "El ID del ticket original es obligatorio.")]
        public int SaleId { get; set; }

        [MaxLength(255, ErrorMessage = "El motivo no puede exceder los 255 caracteres.")]
        public string? Reason { get; set; }

        [Required(ErrorMessage = "La lista de devoluciones no puede estar vacía.")]
        [MinLength(1, ErrorMessage = "Debe haber al menos un artículo en la devolución.")]
        public List<CreateReturnDetailDto> Details { get; set; } = new List<CreateReturnDetailDto>();
    }
}