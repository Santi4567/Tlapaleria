using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class UpdateUserDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string Name { get; set; }

        [Required(ErrorMessage = "El usuario es obligatorio")]
        public string Username { get; set; }

        [Required(ErrorMessage = "El rol es obligatorio")]
        public string RolNombre { get; set; } // Para que puedas cambiarlo de "Vendedor" a "Gerente"

        public bool IsActive { get; set; } // Para poder activar/desactivar usuarios
    }
}