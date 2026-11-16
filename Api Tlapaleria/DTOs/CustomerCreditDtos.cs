using System.ComponentModel.DataAnnotations;

namespace Api_Tlapaleria.DTOs
{
    // =========================================================================
    // 1. DTOs DE CLIENTES (Catálogo e Información de Contacto)
    // =========================================================================
    public class CreateCustomerDto
    {
        [Required(ErrorMessage = "El nombre del cliente es obligatorio.")]
        [MaxLength(150, ErrorMessage = "El nombre no puede exceder los 150 caracteres.")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100, ErrorMessage = "El apodo o referencia no puede exceder los 100 caracteres.")]
        public string? Alias { get; set; }

        [MaxLength(30, ErrorMessage = "El teléfono no puede exceder los 30 caracteres.")]
        public string? Phone { get; set; }

        [MaxLength(30, ErrorMessage = "El teléfono secundario no puede exceder los 30 caracteres.")]
        public string? SecondaryPhone { get; set; }

        [MaxLength(100, ErrorMessage = "El correo no puede exceder los 100 caracteres.")]
        public string? Email { get; set; }

        [MaxLength(255, ErrorMessage = "La dirección no puede exceder los 255 caracteres.")]
        public string? Address { get; set; }

        [MaxLength(500, ErrorMessage = "Las notas no pueden exceder los 500 caracteres.")]
        public string? Notes { get; set; }

        [Range(0, 99999999.99, ErrorMessage = "El límite de crédito no puede ser negativo.")]
        public decimal? CreditLimit { get; set; }
    }

    public class UpdateCustomerDto : CreateCustomerDto
    {
        public bool IsActive { get; set; } = true;
    }

    public class CustomerSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Alias { get; set; }
        public string? Phone { get; set; }
        public string? SecondaryPhone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        public decimal? CreditLimit { get; set; }
        public decimal? AvailableCredit { get; set; } // Si tiene CreditLimit > 0, cuánto margen le queda
        public decimal CurrentDebt { get; set; }      // Total que debe actualmente
        public int ActiveAccountsCount { get; set; }  // Cuántas cuentas/tickets debe hoy
        public int OverdueAccountsCount { get; set; } // Cuántas cuentas tiene vencidas
        public int TotalHistoricalAccountsCount { get; set; }
        public DateTime? LastPurchaseDate { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CustomerFullProfileDto
    {
        public CustomerSummaryDto Customer { get; set; } = new();
        public decimal TotalHistoricalPurchased { get; set; }
        public decimal TotalHistoricalPaid { get; set; }
        public decimal TotalHistoricalReturned { get; set; }
        public decimal TotalCurrentDebt { get; set; }
        public List<CreditAccountDetailDto> Accounts { get; set; } = new();
        public List<CreditPaymentDto> RecentPayments { get; set; } = new();
    }

    // =========================================================================
    // 2. DTOs DE CUENTAS POR COBRAR (Ventas a Crédito / Fiado)
    // =========================================================================
    public class CreditAccountSaleItemDto
    {
        public int SaleDetailId { get; set; }
        public int ProductId { get; set; }
        public int PresentationId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public decimal QuantityTaken { get; set; }    // Cuánto se llevó inicialmente
        public decimal QuantityReturned { get; set; } // Cuánto devolvió (si hubo devolución)
        public decimal NetQuantityKept { get; set; }  // Cuánto se quedó realmente
        public decimal UnitPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal OriginalSubtotal { get; set; } // Importe neto al llevarse el material
        public decimal RefundedSubtotal { get; set; } // Importe descontado por devolución
        public decimal NetSubtotal { get; set; }      // Importe final de este renglón
    }

    public class CreditAccountReturnItemDto
    {
        public int SaleDetailId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal QuantityReturned { get; set; }
        public decimal RefundAmount { get; set; }
    }

    public class CreditAccountReturnDto
    {
        public int ReturnId { get; set; }
        public string ReturnFolio { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public decimal TotalRefunded { get; set; }
        public decimal AppliedToDebtAmount { get; set; } // Lo que descontó de su deuda
        public decimal CashRefundedAmount { get; set; }  // Lo que se le regresó en efectivo (si ya había pagado de más)
        public List<CreditAccountReturnItemDto> Items { get; set; } = new();
    }

    public class CreditPaymentDto
    {
        public int Id { get; set; }
        public string PaymentFolio { get; set; } = string.Empty;
        public int CreditAccountId { get; set; }
        public string CreditAccountFolio { get; set; } = string.Empty;
        public int SaleId { get; set; }
        public string SaleFolio { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? PaymentReference { get; set; }
        public string? Notes { get; set; }
        public string ReceivedByUserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CreditAccountDetailDto
    {
        public int Id { get; set; }
        public string Folio { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Pendiente | Abonado | Liquidado | Cancelado
        public bool IsOverdue { get; set; }

        // Información del Cliente y Contacto
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerAlias { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerSecondaryPhone { get; set; }
        public string? CustomerAddress { get; set; }

        // Información de cuándo adquirió la venta y quién lo atendió
        public int SaleId { get; set; }
        public string SaleFolio { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public string SoldByUserName { get; set; } = string.Empty;
        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }

        // Desglose monetario de la deuda
        public decimal OriginalAmount { get; set; } // Cuánto fue el total al llevarse la venta
        public decimal ReturnedAmount { get; set; } // Cuánto se descontó por devoluciones
        public decimal NetAccountTotal => Math.Max(0m, OriginalAmount - ReturnedAmount);
        public decimal PaidAmount { get; set; }     // Cuánto ha abonado en total
        public decimal Balance { get; set; }        // Lo que debe hoy de esta cuenta

        public DateTime? DueDate { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? PaidAt { get; set; }

        // Qué se llevó y cuánto se llevó
        public int TotalItemsCount { get; set; }
        public List<CreditAccountSaleItemDto> Items { get; set; } = new();

        // Abonos hechos a esta cuenta
        public List<CreditPaymentDto> Payments { get; set; } = new();

        // Devoluciones hechas sobre esta cuenta
        public List<CreditAccountReturnDto> Returns { get; set; } = new();
    }

    // =========================================================================
    // 3. DTOs PARA ABONAR Y ADMINISTRAR CUENTAS
    // =========================================================================
    public class RegisterCreditPaymentDto
    {
        [Required(ErrorMessage = "El monto a abonar es obligatorio.")]
        [Range(0.01, 9999999.99, ErrorMessage = "El monto del abono debe ser mayor a $0.00.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "El método de pago del abono es obligatorio.")]
        [RegularExpression("^(Efectivo|Tarjeta|Transferencia)$", ErrorMessage = "El método de pago del abono debe ser Efectivo, Tarjeta o Transferencia.")]
        public string PaymentMethod { get; set; } = "Efectivo";

        [MaxLength(100, ErrorMessage = "La referencia no puede exceder los 100 caracteres.")]
        public string? PaymentReference { get; set; }

        [MaxLength(255, ErrorMessage = "La nota no puede exceder los 255 caracteres.")]
        public string? Notes { get; set; }
    }

    public class UpdateCreditAccountDto
    {
        public DateTime? DueDate { get; set; }

        [MaxLength(255, ErrorMessage = "La nota no puede exceder los 255 caracteres.")]
        public string? Notes { get; set; }
    }

    public class GeneralCustomerPaymentResultDto
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmountApplied { get; set; }
        public decimal PreviousTotalDebt { get; set; }
        public decimal RemainingTotalDebt { get; set; }
        public int AccountsAffectedCount { get; set; }
        public int AccountsSettledCount { get; set; }
        public List<CreditPaymentDto> AppliedPayments { get; set; } = new();
    }

    public class CreditPortfolioSummaryDto
    {
        public decimal TotalReceivableBalance { get; set; } // Total que nos deben en la calle
        public int CustomersWithDebtCount { get; set; }     // Clientes que deben actualmente
        public int ActiveAccountsCount { get; set; }        // Total de tickets con saldo pendiente
        public int OverdueAccountsCount { get; set; }       // Tickets vencidos (pasaron de su DueDate)
        public decimal OverdueBalance { get; set; }         // Monto total en cuentas vencidas
        public decimal CreditGrantedTodayAmount { get; set; } // Fiado otorgado hoy
        public decimal CollectedTodayAmount { get; set; }     // Abonos cobrados hoy
    }
}
