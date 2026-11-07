using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api_Tlapaleria.Models
{
    [Table("BusinessConfigs")]
    public class BusinessConfig
    {
        [Key]
        public int Id { get; set; } = 1;

        [Required]
        [MaxLength(150)]
        public string BusinessName { get; set; } = "Tlapalería y Materiales";

        [MaxLength(20)]
        public string? Rfc { get; set; }

        [Required]
        [MaxLength(10)]
        public string PostalCode { get; set; } = "72310"; // Código postal del emisor (SAT)

        [MaxLength(250)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(150)]
        public string? Email { get; set; }

        public bool FacturacionActiva { get; set; } = true;

        [MaxLength(200)]
        public string? FacturapiApiKey { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
