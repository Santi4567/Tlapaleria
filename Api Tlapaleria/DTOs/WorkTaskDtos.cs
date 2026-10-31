using System.ComponentModel.DataAnnotations;
using Api_Tlapaleria.Enums;

namespace Api_Tlapaleria.DTOs
{
    public class WorkTaskDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Priority { get; set; } = "Normal";
        public string Status { get; set; } = "Pendiente";
        public int AssignedToUserId { get; set; }
        public string AssignedToUserName { get; set; } = string.Empty;
        public int AssignedByUserId { get; set; }
        public string AssignedByUserName { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public string? WorkerNotes { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateWorkTaskDto
    {
        [Required(ErrorMessage = "El título de la tarea es obligatorio.")]
        [MaxLength(200, ErrorMessage = "El título no puede exceder los 200 caracteres.")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000, ErrorMessage = "La descripción es demasiado larga.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "La prioridad es obligatoria.")]
        [EnumDataType(typeof(WorkTaskPriority), ErrorMessage = "Prioridad inválida. Usa: Baja, Normal o Urgente.")]
        public string Priority { get; set; } = "Normal";

        [Required(ErrorMessage = "Debes seleccionar el trabajador al que se asignará la tarea.")]
        public int AssignedToUserId { get; set; }

        public DateTime? DueDate { get; set; }
    }

    public class UpdateWorkTaskDto
    {
        [Required(ErrorMessage = "El título de la tarea es obligatorio.")]
        [MaxLength(200, ErrorMessage = "El título no puede exceder los 200 caracteres.")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000, ErrorMessage = "La descripción es demasiado larga.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "La prioridad es obligatoria.")]
        [EnumDataType(typeof(WorkTaskPriority), ErrorMessage = "Prioridad inválida. Usa: Baja, Normal o Urgente.")]
        public string Priority { get; set; } = "Normal";

        [Required(ErrorMessage = "Debes seleccionar el trabajador asignado.")]
        public int AssignedToUserId { get; set; }

        public DateTime? DueDate { get; set; }

        [EnumDataType(typeof(WorkTaskStatus), ErrorMessage = "Estado inválido. Usa: Pendiente, EnProgreso, Completada o Cancelada.")]
        public string? Status { get; set; }

        [MaxLength(500)]
        public string? WorkerNotes { get; set; }
    }

    public class UpdateWorkTaskStatusDto
    {
        [Required(ErrorMessage = "El nuevo estado es obligatorio.")]
        [EnumDataType(typeof(WorkTaskStatus), ErrorMessage = "Estado inválido. Usa: Pendiente, EnProgreso o Completada.")]
        public string Status { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "La nota del trabajador no puede exceder 500 caracteres.")]
        public string? WorkerNotes { get; set; }
    }

    public class WorkWorkerItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
    }
}
