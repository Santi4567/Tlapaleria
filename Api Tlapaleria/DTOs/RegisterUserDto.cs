using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class RegisterUserDto
    {
        [Required(ErrorMessage = "El usuario es obligatorio")]
        public string Username { get; set; }

        [Required]
        [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
        [RegularExpression(@"^(?=.*\d)(?=.*[\W_]).+$", ErrorMessage = "La contraseña debe contener al menos un número y un símbolo (ej. 13@#$&)")]
        public string Password { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        // OJO: El frontend manda el nombre ("Admin", "Vendedor"), no el número.
        public string RolNombre { get; set; }
    }
}