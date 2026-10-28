using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class UpdatePresentationDto
    {
        public int? Id { get; set; }

        [Required]
        public string Name { get; set; }
        public string? Code { get; set; }
        public string? Barcode { get; set; }

        // CAMBIO 1: Permitimos 0 cuando la presentación no sea de venta al público
        [Required]
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal SupplierPrice { get; set; }

        [Required]
        [Range(0.0001, double.MaxValue, ErrorMessage = "El factor de stock debe ser mayor a 0.")]
        public decimal StockFactor { get; set; }

        // CAMBIO 2: Controla si se vende en mostrador (true) o es solo referencia/inactiva (false)
        public bool IsActive { get; set; } = true;
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

        public decimal? ProfitMargin { get; set; }

        [Required]
        public string UnitOfMeasure { get; set; }

        public bool AllowFractions { get; set; } = false;

        [Required]
        public bool IsInventoryTracked { get; set; }

        public bool HasExpiration { get; set; } = false;
        public DateTime? NextExpirationDate { get; set; }

        // Aquí NO actualizamos el CurrentStock. El stock solo debe modificarse 
        // a través de compras a proveedores o ventas, no editando el producto.

        [Required]
        public List<UpdatePresentationDto> Presentations { get; set; } = new List<UpdatePresentationDto>();
    }
}