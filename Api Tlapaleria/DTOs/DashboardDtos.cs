namespace Api_Tlapaleria.DTOs
{
    public class PaymentMethodSummaryDto
    {
        public string Method { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int Count { get; set; }
    }

    public class DashboardTopProductDto
    {
        public string ProductName { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public decimal TotalQuantity { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class StockAlertDto
    {
        public int ProductId { get; set; }
        public string InternalCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal CurrentStock { get; set; }
        public string UnitOfMeasure { get; set; } = string.Empty;
        public string AlertType { get; set; } = "LowStock"; // "OutOfStock" | "LowStock" | "ExpiringSoon"
        public string Message { get; set; } = string.Empty;
        public int? DaysRemaining { get; set; }
    }

    public class DashboardSummaryDto
    {
        public decimal TotalSalesAmount { get; set; }
        public decimal GrossSalesAmount { get; set; }
        public decimal TotalRefundedAmount { get; set; }
        public int TotalTicketsCount { get; set; }
        public bool IsGlobal { get; set; } // true if admin / view.sales; false if cashier personal
        public string ScopeLabel { get; set; } = string.Empty;
        public List<PaymentMethodSummaryDto> PaymentMethods { get; set; } = new();
        public List<DashboardTopProductDto> TopSellingProducts { get; set; } = new();
        public List<StockAlertDto> StockAlerts { get; set; } = new();
    }
}
