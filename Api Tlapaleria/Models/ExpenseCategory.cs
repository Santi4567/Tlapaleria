using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Api_Tlapaleria.Enums;

namespace Api_Tlapaleria.Models
{
    [Table("ExpenseCategories")]
    public class ExpenseCategory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        public string Type { get; set; } = ExpenseCategoryType.Operativo.ToString();
        
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}