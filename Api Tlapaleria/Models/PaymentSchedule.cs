using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Api_Tlapaleria.Enums;

namespace Api_Tlapaleria.Models
{
    [Table("PaymentSchedules")]
    public class PaymentSchedule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Column("accounts_payable_id")]
        public int AccountsPayableId { get; set; }
        [ForeignKey("AccountsPayableId")]
        public AccountsPayable? AccountsPayable { get; set; }

        [Required]
        [Column("installment_number")]
        public int InstallmentNumber { get; set; }

        [Required]
        [Column("due_date")]
        public DateTime DueDate { get; set; }

        [Column("amount", TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [Required]
        public string Status { get; set; } = PaymentScheduleStatus.Pendiente.ToString();

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}