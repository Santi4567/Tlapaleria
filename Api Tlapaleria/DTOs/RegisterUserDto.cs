using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class RegisterUserDto
    {
        [Required(ErrorMessage = "El usuario es obligatorio")]
        public string Username { get; set; }

        [Required]
        [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8   caracteres")]
        public string Password { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        [RegularExpression("Admin|Vendedor", ErrorMessage = "El rol solo puede ser Admin o Vendedor")]
        public string Rol { get; set; }
    }
}