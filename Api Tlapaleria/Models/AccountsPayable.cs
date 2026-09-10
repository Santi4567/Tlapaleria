using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Api_Tlapaleria.Enums;

namespace Api_Tlapaleria.Models
{
    [Table("AccountsPayable")]
    public class AccountsPayable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Column("supplier_id")]
        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; }

        [Required]
        [MaxLength(255)]
        public string Concept { get; set; }

        [Column("total_amount", TypeName = "decimal(12,2)")]
        public decimal TotalAmount { get; set; }

        [Column("balance", TypeName = "decimal(12,2)")]
        public decimal Balance { get; set; }

        [Column("due_date")]
        public DateTime? DueDate { get; set; }

        [Column("payment_frequency_days")]
        public int? PaymentFrequencyDays { get; set; }

        [Required]
        public string Status { get; set; } = AccountsPayableStatus.Pendiente.ToString();

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public List<PaymentSchedule> PaymentSchedules { get; set; } = new List<PaymentSchedule>();
    }
}