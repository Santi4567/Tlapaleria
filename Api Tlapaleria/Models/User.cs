using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization; // Necesario para JsonIgnore

namespace Api_Tlapaleria.Models
{
    [Table("users")] // Asegura que EF busque la tabla "users" y no "user"
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [JsonIgnore] // ¡Importante! Evita que la contraseña (hash) salga en las respuestas JSON
        public string Passwd { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Rol { get; set; } = string.Empty; // Aquí guardarás "Admin" o "Vendedor"
    }
}