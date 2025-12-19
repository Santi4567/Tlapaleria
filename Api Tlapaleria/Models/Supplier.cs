using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api_Tlapaleria.Models
{
    [Table("suppliers")] // Mapea a la tabla que acabamos de crear
    public class Supplier
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty; // Nombre de la Empresa

        [Required]
        [MaxLength(100)]
        public string ContactName { get; set; } = string.Empty; // "Nombre Vendedor"

        [Required]
        [MaxLength(20)] // Suficiente para celulares o fijos
        [Phone] // Validación extra de formato telefónico
        public string Phone { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}