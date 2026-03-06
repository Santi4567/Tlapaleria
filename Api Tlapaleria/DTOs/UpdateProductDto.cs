using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class UpdatePresentationDto
    {
        // Si el Id es 0 o nulo, significa que es una presentación NUEVA
        // Si trae un Id, significa que vamos a ACTUALIZARLA
        public int? Id { get; set; }

        [Required]
        public string Name { get; set; }
        public string? Code { get; set; }
        public string? Barcode { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Price { get; set; }

        [Required]
        public decimal StockFactor { get; set; }
    }

    public class UpdateProductDto
    {
        [Required]
        public string InternalCode { get; set; }
        public string? Barcode { get; set; }

        [Required]
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? Brand { get; set; }
        public string? Location { get; set; }

        [Required]
        public int SupplierId { get; set; }

        [Required]
        public decimal SupplierPrice { get; set; }
        public decimal? ProfitMargin { get; set; }

        [Required]
        public string UnitOfMeasure { get; set; }

        // Aquí NO actualizamos el CurrentStock. El stock solo debe modificarse 
        // a través de compras a proveedores o ventas, no editando el producto.

        [Required]
        public List<UpdatePresentationDto> Presentations { get; set; } = new List<UpdatePresentationDto>();
    }
}