namespace Api_Tlapaleria.DTOs
{
    // ==============================================================================
    // -- REPORTE DE VENTAS POR USUARIO (QUÉ VENDIÓ CADA USUARIO) --
    // ==============================================================================

    public class UserProductSalesItemDto
    {
        public int ProductId { get; set; }
        public int PresentationId { get; set; }
        public string InternalCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string PresentationName { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public int TicketsCount { get; set; }
        public decimal QuantitySold { get; set; }
        public decimal QuantityReturned { get; set; }
        public decimal NetQuantity { get; set; }
        public decimal UnitSupplierCost { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal RefundedAmount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal RealProfitAmount { get; set; }
    }

    public class UserSalesReportItemDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int TicketsCount { get; set; }
        public decimal GrossSalesAmount { get; set; }
        public decimal TotalRefundedAmount { get; set; }
        public decimal NetSalesAmount { get; set; }
        public decimal CostOfGoodsSold { get; set; }
        public decimal RealProfitAmount { get; set; }
        public decimal ProfitMarginPercent { get; set; }
        public decimal TotalQuantitySold { get; set; }
        public decimal TotalQuantityReturned { get; set; }
        public decimal NetQuantitySold { get; set; }
        public List<PaymentMethodSummaryDto> PaymentMethodsSummary { get; set; } = new();
        public List<UserProductSalesItemDto> Products { get; set; } = new();
    }

    public class UserSalesReportResponseDto
    {
        public string PeriodType { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int TotalUsersCount { get; set; }
        public int TotalTicketsCount { get; set; }
        public decimal TotalGrossSales { get; set; }
        public decimal TotalRefundedAmount { get; set; }
        public decimal TotalNetSales { get; set; }
        public decimal TotalCostOfGoodsSold { get; set; }
        public decimal TotalProfitAmount { get; set; }
        public decimal TotalQuantitySold { get; set; }
        public List<PaymentMethodSummaryDto> PaymentMethodsSummary { get; set; } = new();
        public List<UserSalesReportItemDto> Users { get; set; } = new();
    }

    public class UserSalesDetailResponseDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string PeriodType { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int TicketsCount { get; set; }
        public decimal GrossSalesAmount { get; set; }
        public decimal TotalRefundedAmount { get; set; }
        public decimal NetSalesAmount { get; set; }
        public decimal CostOfGoodsSold { get; set; }
        public decimal RealProfitAmount { get; set; }
        public decimal ProfitMarginPercent { get; set; }
        public decimal TotalQuantitySold { get; set; }
        public decimal TotalQuantityReturned { get; set; }
        public decimal NetQuantitySold { get; set; }
        public List<PaymentMethodSummaryDto> PaymentMethodsSummary { get; set; } = new();
        public List<UserProductSalesItemDto> Products { get; set; } = new();
        public List<FinancialSaleItemDto> Sales { get; set; } = new();
    }
}
