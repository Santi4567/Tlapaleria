using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    // ====================================================================
    // 1. DTO para registrar un EGRESO (Dinero que sale de caja/banco)
    // Sirve tanto para compras de contado como para abonos a deudas.
    // ====================================================================
    public class CreateExpenseDto
    {
        [Required(ErrorMessage = "El concepto del gasto es obligatorio")]
        [MaxLength(255, ErrorMessage = "El concepto no puede superar los 255 caracteres")]
        public string Concept { get; set; } // Ej: "Pago recibo CFE", "Abono factura Truper"

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "El método de pago es obligatorio")]
        public string PaymentMethod { get; set; } // "Efectivo", "Transferencia", "Tarjeta", "Cheque"

        [Required(ErrorMessage = "Debes clasificar el tipo de gasto (Categoría)")]
        public int CategoryId { get; set; }

        // --- CAMPOS OPCIONALES SEGÚN EL TIPO DE GASTO ---

        // Se llena si el gasto está ligado a un proveedor (sea de contado o a crédito)
        public int? SupplierId { get; set; }

        // Se llena ÚNICAMENTE si este egreso es el abono a una deuda existente
        public int? AccountsPayableId { get; set; }

        // Se llena si el abono va dirigido a una cuota sugerida en específico
        public int? ScheduleId { get; set; }

        // --- COMPROBANTES ---
        public string? ReceiptNumber { get; set; } // Ej: Folio del ticket o factura
        public string? ReceiptUrl { get; set; } // Para cuando suban la foto del ticket
    }

    // ====================================================================
    // 2. DTO para registrar una DEUDA (Mercancía a crédito)
    // Aquí NO sale dinero, solo se registra el compromiso de pago.
    // ====================================================================
    public class CreateAccountsPayableDto
    {
        [Required(ErrorMessage = "Debes seleccionar el proveedor al que se le debe")]
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "El concepto de la deuda es obligatorio")]
        [MaxLength(255, ErrorMessage = "El concepto no puede superar los 255 caracteres")]
        public string Concept { get; set; } // Ej: "Factura #1024 - Herramienta variada"

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto total de la deuda debe ser mayor a 0")]
        public decimal TotalAmount { get; set; }

        // Fecha límite general que da el proveedor (Ej: Tienes 30 días para liquidar todo)
        public DateTime? DueDate { get; set; }

        // Cada cuántos días se pactó abonar (Ej: 7 = Semanal, 15 = Quincenal).
        // Si viene NULL o 0, el sistema entenderá que es Crédito con Abonos Libres (sin plan de pagos).
        [Range(1, 365, ErrorMessage = "La frecuencia de pago debe estar entre 1 y 365 días")]
        public int? PaymentFrequencyDays { get; set; }
    }
}