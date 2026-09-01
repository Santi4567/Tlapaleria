namespace Api_Tlapaleria.DTOs
{
    // Representa un punto individual en tu gráfica (por ejemplo, un día)
    public class ChartDataPointDto
    {
        public string DateLabel { get; set; } = string.Empty;
        public int SalesCount { get; set; }
        public decimal NetAmount { get; set; }
    }

    // El objeto final que el endpoint devolverá
    public class FinancialReportDto
    {
        public int TotalSalesCount { get; set; }
        public decimal GrossSalesAmount { get; set; }
        public decimal TotalRefundedAmount { get; set; }
        public decimal NetSalesAmount { get; set; }

        public List<ChartDataPointDto> ChartData { get; set; } = new List<ChartDataPointDto>();
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
        public decimal? SupplierPrice { get; set; }
        public Dictionary<int, decimal?> PresentationPrices { get; set; } = new(); // key: PresentationId
    }

    // -- FIN de  Reporte de precios para los productos ---
}