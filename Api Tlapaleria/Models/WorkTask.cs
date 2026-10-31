using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Api_Tlapaleria.Enums;

namespace Api_Tlapaleria.Models
{
    [Table("WorkTasks")]
    public class WorkTask
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Required]
        [Column("priority")]
        public string Priority { get; set; } = WorkTaskPriority.Normal.ToString();

        [Required]
        [Column("status")]
        public string Status { get; set; } = WorkTaskStatus.Pendiente.ToString();

        [Required]
        [Column("assigned_to_user_id")]
        public int AssignedToUserId { get; set; }
        [ForeignKey("AssignedToUserId")]
        public User? AssignedToUser { get; set; }

        [Required]
        [Column("assigned_by_user_id")]
        public int AssignedByUserId { get; set; }
        [ForeignKey("AssignedByUserId")]
        public User? AssignedByUser { get; set; }

        [Column("due_date")]
        public DateTime? DueDate { get; set; }

        [MaxLength(500)]
        [Column("worker_notes")]
        public string? WorkerNotes { get; set; }

        [Column("started_at")]
        public DateTime? StartedAt { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
