using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api_Tlapaleria.Models
{
    public class UserSession
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public string RefreshToken { get; set; } // Aquí guardaremos el Hash encriptado

        [Required]
        public DateTime ExpiryTime { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }
    }
}