using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class RegisterUserDto
    {
        [Required(ErrorMessage = "El usuario es obligatorio")]
        public string Username { get; set; }

        [Required]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
        public string Password { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        // OJO: El frontend manda el nombre ("Admin", "Vendedor"), no el número.
        public string RolNombre { get; set; }
    }
}