using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Api_Tlapaleria.Models
{
    [Table("ReturnDetails")]
    public class ReturnDetail
    {
        [Key]
        public int Id { get; set; }

        // Relación con la Cabecera de la Devolución
        [Required]
        public int ReturnId { get; set; }

        [JsonIgnore]
        [ForeignKey("ReturnId")]
        public SaleReturn? Return { get; set; }

        // EL ANCLA: Relación con el renglón específico del ticket original
        [Required]
        public int SaleDetailId { get; set; }

        [JsonIgnore]
        [ForeignKey("SaleDetailId")]
        public SaleDetail? SaleDetail { get; set; }

        [Required]
        public int QuantityReturned { get; set; } // Unidades enteras devueltas

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal RefundAmount { get; set; } // Dinero regresado por este concepto
    }
}