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
}