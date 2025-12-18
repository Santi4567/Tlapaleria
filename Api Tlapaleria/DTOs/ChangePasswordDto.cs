using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "Debes ingresar tu contraseña actual")]
        public string CurrentPassword { get; set; }

        [Required]
        [MinLength(8, ErrorMessage = "La nueva contraseña debe tener al menos 8 caracteres")]
        [RegularExpression(@"^(?=.*\d)(?=.*[\W_]).+$", ErrorMessage = "La contraseña debe contener al menos un número y un símbolo (ej. @#$&)")]
        public string NewPassword { get; set; }
    }
}