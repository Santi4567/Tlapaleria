using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    // Para CREAR
    public class CreateSupplierDto
    {
        [Required(ErrorMessage = "El nombre de la empresa es obligatorio")]
        [MaxLength(100)]
        public string Name { get; set; } // "Truper"

        [Required(ErrorMessage = "El nombre del vendedor es obligatorio")]
        [MaxLength(100)]
        public string ContactName { get; set; } // "Roberto Ventas"

        [Required(ErrorMessage = "El teléfono es obligatorio")]
        [Phone(ErrorMessage = "Formato de teléfono inválido")]
        [MaxLength(20)]
        public string Phone { get; set; }
    }

    // Para EDITAR (Casi igual, pero separado por si la lógica cambia futuro)
    public class UpdateSupplierDto : CreateSupplierDto
    {
        public bool IsActive { get; set; } // Permitimos reactivar si estaba borrado
    }
}