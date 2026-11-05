using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class QuickPresentationPriceDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "El precio de venta no puede ser negativo.")]
        public decimal Price { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "El costo de proveedor no puede ser negativo.")]
        public decimal SupplierPrice { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class QuickPriceUpdateDto
    {
        [Required(ErrorMessage = "El proveedor es obligatorio.")]
        public int SupplierId { get; set; }

        [Range(0, 1000, ErrorMessage = "El margen de ganancia debe estar entre 0% y 1000%.")]
        public decimal? ProfitMargin { get; set; }

        [Required(ErrorMessage = "Debes incluir las presentaciones a actualizar.")]
        public List<QuickPresentationPriceDto> Presentations { get; set; } = new();
    }
}
