namespace Api_Tlapaleria.DTOs
{
    public class StockUpdateNotificationDto
    {
        public int ProductId { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal ReservedStock { get; set; }
        public decimal AvailableStock { get; set; }
    }

    public class ReserveStockRequestDto
    {
        public int PresentationId { get; set; }
        public decimal Quantity { get; set; }
    }

    public class StockReservationResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public int PresentationId { get; set; }
        public decimal RequestedQuantity { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal ReservedStock { get; set; }
        public decimal AvailableStock { get; set; }
    }
}
