using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    // Para devolver la información a React
    public class RolDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public List<int> PermisosIds { get; set; } = new(); // IDs para marcar los checkboxes en React
        public List<string> PermisosNombres { get; set; } = new(); // Nombres visuales
    }

    // Para Crear o Editar un Rol desde React
    public class CreateEditRolDto
    {
        [Required(ErrorMessage = "El nombre del rol es obligatorio")]
        [MaxLength(50)]
        public string Nombre { get; set; } = string.Empty;

        // Lista de IDs de los permisos que el admin seleccionó en los checkboxes
        public List<int> PermisosIds { get; set; } = new();
    }
    public class UpdateRolNameDto
    {
        [Required(ErrorMessage = "El nombre del rol es obligatorio")]
        [MaxLength(50)]
        public string Nombre { get; set; } = string.Empty;
    }
}