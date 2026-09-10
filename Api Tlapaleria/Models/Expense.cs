using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Api_Tlapaleria.Enums;

namespace Api_Tlapaleria.Models
{
    [Table("Expenses")]
    public class Expense
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Concept { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [Column("expense_date")]
        public DateTime ExpenseDate { get; set; } = DateTime.Now;

        [Required]
        [Column("payment_method")]
        public string PaymentMethod { get; set; }

        [Required]
        [Column("category_id")]
        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public ExpenseCategory? Category { get; set; }

        [Column("supplier_id")]
        public int? SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; }

        [Column("accounts_payable_id")]
        public int? AccountsPayableId { get; set; }
        [ForeignKey("AccountsPayableId")]
        public AccountsPayable? AccountsPayable { get; set; }

        [Column("schedule_id")]
        public int? ScheduleId { get; set; }
        [ForeignKey("ScheduleId")]
        public PaymentSchedule? Schedule { get; set; }

        [MaxLength(100)]
        [Column("receipt_number")]
        public string? ReceiptNumber { get; set; }

        [MaxLength(500)]
        [Column("receipt_url")]
        public string? ReceiptUrl { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}