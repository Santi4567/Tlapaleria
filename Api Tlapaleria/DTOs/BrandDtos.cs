using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    // Lo que el front recibe en las sugerencias
    public record BrandDto(int Id, string Name);

    // Fusionar: migra todos los productos de la marca origen a la destino
    // y elimina la marca origen. Ej: "Truuper" (origen) -> "Truper" (destino)
    public class MergeBrandsDto
    {
        [Required]
        public int SourceBrandId { get; set; }

        [Required]
        public int TargetBrandId { get; set; }
    }

    // Renombrar: corrige el nombre de una marca y de todos sus productos
    public class RenameBrandDto
    {
        [Required(ErrorMessage = "El nombre de la marca es obligatorio")]
        [StringLength(100, ErrorMessage = "El nombre no puede pasar de 100 caracteres")]
        public string Name { get; set; } = null!;
    }
}
