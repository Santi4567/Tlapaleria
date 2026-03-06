using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    // 1. EL DTO DEL HIJO (Presentación)
    public class CreatePresentationDto
    {
        [Required(ErrorMessage = "El nombre de la presentación es obligatorio")]
        public string Name { get; set; } // "Bolsa 1kg"

        public string? Code { get; set; } // Opcional
        public string? Barcode { get; set; } // Opcional

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0")]
        public decimal Price { get; set; } // $45.00

        [Required]
        public decimal StockFactor { get; set; } // 1.0 (cuánto descuenta del padre)
    }

    // 2. EL DTO DEL PADRE (Producto General)
    public class CreateProductDto
    {
        // --- Identificación ---
        [Required]
        public string InternalCode { get; set; } // "CLA-2-STD"
        public string? Barcode { get; set; } // Código del empaque master

        [Required]
        public string Name { get; set; } // "Clavo 2 Pulgadas"
        public string? Description { get; set; }
        public string? Brand { get; set; }
        public string? Location { get; set; } // "Pasillo 4"

        // --- Proveedor y Costos ---
        [Required]
        public int SupplierId { get; set; }

        [Required]
        public decimal SupplierPrice { get; set; } // Costo ($20)

        public decimal? ProfitMargin { get; set; } // Ganancia sugerida (30%)

        // --- Inventario ---
        [Required]
        public string UnitOfMeasure { get; set; } // "KG"

        public decimal InitialStock { get; set; } // Stock inicial al crearlo (ej: 50)

        // --- LA LISTA DE HIJOS ---
        // Aquí es donde recibes todas las variantes de golpe
        [Required]
        [MinLength(1, ErrorMessage = "Debes agregar al menos una forma de venta (Presentación)")]
        public List<CreatePresentationDto> Presentations { get; set; }
    }
}