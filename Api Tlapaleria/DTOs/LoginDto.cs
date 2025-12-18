using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    public class LoginDto
    {
        [Required]
        public string UsuarioOCorreo { get; set; } // Acepta user o email

        [Required]
        public string Password { get; set; }
    }
}