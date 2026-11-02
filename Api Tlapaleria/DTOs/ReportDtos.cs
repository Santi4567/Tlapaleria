namespace Api_Tlapaleria.DTOs
{
    // Representa un punto individual en tu gráfica (por ejemplo, un día)
    public class ChartDataPointDto
    {
        public string DateLabel { get; set; } = string.Empty;
        public int SalesCount { get; set; }
        public decimal NetAmount { get; set; }

        public decimal RealProfitAmount { get; set; }
    }

    // El objeto final que el endpoint devolverá
    // El objeto final que el endpoint devolverá
    public class FinancialReportDto
    {
        public int TotalSalesCount { get; set; }
        public decimal GrossSalesAmount { get; set; }
        public decimal TotalRefundedAmount { get; set; }
        public decimal NetSalesAmount { get; set; }

        // --- Ganancia real (ventas netas - costo proveedor) ---
        public decimal CostOfGoodsSold { get; set; }
        public decimal RealProfitAmount { get; set; }

        public List<ChartDataPointDto> ChartData { get; set; } = new List<ChartDataPointDto>();
        public List<FinancialSaleItemDto> Sales { get; set; } = new List<FinancialSaleItemDto>();
        public List<FinancialReturnItemDto> Returns { get; set; } = new List<FinancialReturnItemDto>();
        public List<TopSellingProductDto> TopProducts { get; set; } = new List<TopSellingProductDto>();
    }

    public class FinancialSaleItemDto
    {
        public int SaleId { get; set; }
        public string Folio { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string? PaymentReference { get; set; }
        public int ItemsCount { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal RefundedAmount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal RealProfitAmount { get; set; }
        public bool IsActive { get; set; }
    }

    public class FinancialReturnItemDetailDto
    {
        public int SaleDetailId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal QuantityReturned { get; set; }
        public decimal RefundAmount { get; set; }
    }

    public class FinancialReturnItemDto
    {
        public int ReturnId { get; set; }
        public string ReturnFolio { get; set; } = string.Empty;
        public int SaleId { get; set; }
        public string SaleFolio { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public decimal TotalRefunded { get; set; }
        public bool IsFullCancellation { get; set; }
        public List<FinancialReturnItemDetailDto> Items { get; set; } = new();
    }

    public class TopSellingProductDto
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
        public decimal NetSalesAmount { get; set; }
        public decimal RealProfitAmount { get; set; }
    }

    // -- Para el reporte de precios de un producto --
    public class ProductPriceHistoryDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public List<PresentationInfoDto> Presentations { get; set; } = new();
        public List<PriceHistoryRowDto> History { get; set; } = new();
    }

    public class PresentationInfoDto
    {
        public int PresentationId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class PriceHistoryRowDto
    {
        public DateTime Date { get; set; }
        public Dictionary<int, decimal?> PresentationPrices { get; set; } = new();
        public Dictionary<int, decimal?> PresentationSupplierPrices { get; set; } = new();
        public bool Actual { get; set; } = false; // true solo en el nodo sintético de "hoy" (Caso 1)
    }

    // -- Para el reporte de corte/ventas por proveedor --
    public class SupplierSalesReportItemDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public int EffectiveTickets { get; set; }
        public decimal SupplierAmount { get; set; }      // DineroProveedor
        public decimal TotalChargedClient { get; set; }  // TotalCobradoCliente
        public decimal ProfitAmount { get; set; }        // TuGanancia
    }

    public class SupplierSalesReportResponseDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal TotalSupplierAmount { get; set; }
        public decimal TotalChargedClient { get; set; }
        public decimal TotalProfitAmount { get; set; }
        public List<SupplierSalesReportItemDto> Suppliers { get; set; } = new();
    }

    // -- Desglose por producto/presentación de lo vendido de un proveedor específico --
    public class SupplierProductSalesItemDto
    {
        public int ProductId { get; set; }
        public int PresentationId { get; set; }
        public string InternalCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string PresentationName { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public int EffectiveTickets { get; set; }
        public decimal QuantitySold { get; set; }
        public decimal QuantityReturned { get; set; }
        public decimal NetQuantity { get; set; }
        public decimal UnitSupplierCost { get; set; }
        public decimal UnitPublicPrice { get; set; }
        public decimal SupplierAmount { get; set; }      // DineroProveedor
        public decimal TotalChargedClient { get; set; }  // TotalCobradoCliente
        public decimal ProfitAmount { get; set; }        // TuGanancia
    }

    public class SupplierSalesDetailResponseDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int EffectiveTickets { get; set; }
        public decimal TotalNetQuantity { get; set; }
        public decimal TotalSupplierAmount { get; set; }
        public decimal TotalChargedClient { get; set; }
        public decimal TotalProfitAmount { get; set; }
        public List<SupplierProductSalesItemDto> Products { get; set; } = new();
    }

    // -- FIN de  Reporte de precios para los productos ---
}