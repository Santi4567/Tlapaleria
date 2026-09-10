using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Api_Tlapaleria.Enums;

namespace Api_Tlapaleria.Models
{
    [Table("PaymentReminders")]
    public class PaymentReminder
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Column("schedule_id")]
        public int ScheduleId { get; set; }
        [ForeignKey("ScheduleId")]
        public PaymentSchedule? Schedule { get; set; }

        [Required]
        [Column("remind_at")]
        public DateTime RemindAt { get; set; }

        [Required]
        public string Channel { get; set; } = ReminderChannel.InApp.ToString();

        [Required]
        public string Status { get; set; } = ReminderStatus.Pendiente.ToString();

        [Column("sent_at")]
        public DateTime? SentAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}