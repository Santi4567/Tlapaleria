using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Api_Tlapaleria.Models
{
    [Table("Customers")]
    public class Customer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del cliente es obligatorio.")]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Alias { get; set; } // Ej. "Don Pepe el Albañil", "Maestro Plomero"

        [MaxLength(30)]
        public string? Phone { get; set; }

        [MaxLength(30)]
        public string? SecondaryPhone { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; } // Referencias de confianza, oficio, quién lo recomendó

        [Column(TypeName = "decimal(12,2)")]
        public decimal? CreditLimit { get; set; } // Límite de crédito opcional (null o 0 = sin límite fijo)

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Propiedades calculadas en tiempo de consulta para mostrar rápido cuánto debe
        [NotMapped]
        public decimal CurrentDebt { get; set; }

        [NotMapped]
        public int ActiveAccountsCount { get; set; }

        [JsonIgnore]
        public List<CreditAccount> CreditAccounts { get; set; } = new List<CreditAccount>();
    }
}
