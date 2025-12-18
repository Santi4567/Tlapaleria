using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization; // Necesario para evitar bucles infinitos al convertir a JSON

namespace Api_Tlapaleria.Models
{
    public class Permiso
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string NombreSistema { get; set; } // Aquí guardaremos "add.users", "view.products"

        [MaxLength(200)]
        public string Descripcion { get; set; } // Ejemplo: "Permite registrar usuarios"

        // Relación Muchos a Muchos: Un Permiso está en muchos Roles
        [JsonIgnore]
        public List<Rol> Roles { get; set; } = new List<Rol>();
    }
}