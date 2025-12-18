using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class AdminResetPasswordDto
    {
        [Required]
        [MinLength(8, ErrorMessage = "La nueva contraseña debe tener al menos 8 caracteres")]
        [RegularExpression(@"^(?=.*\d)(?=.*[\W_]).+$", ErrorMessage = "La contraseña debe contener al menos un número y un símbolo")]
        public string NewPassword { get; set; }
    }
}