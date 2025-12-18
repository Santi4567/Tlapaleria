using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api_Tlapaleria.Models
{
    public class Rol
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Nombre { get; set; } // "Admin", "Vendedor", "Gerente"

        // Relación Muchos a Muchos: Un Rol tiene muchos Permisos
        [JsonIgnore]
        public List<Permiso> Permisos { get; set; } = new List<Permiso>();
    }
}