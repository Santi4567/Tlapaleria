using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models;

namespace Api_Tlapaleria.Services
{
    public interface IStockReservationService
    {
        Task<StockReservationResultDto> SetItemReservationAsync(string connectionId, int userId, int presentationId, decimal quantity);
        Task RemoveItemReservationAsync(string connectionId, int presentationId);
        Task ClearCartAsync(string connectionId);
        decimal GetReservedStock(int productId);
        decimal GetReservedStockExcluding(int productId, int userId, string? connectionId = null);
        void PopulateReservedStock(Product product);
        void PopulateReservedStock(IEnumerable<Product> products);
        Task ConsumeReservationsOnSaleAsync(int userId, string? connectionId, IEnumerable<(int ProductId, int PresentationId, decimal QuantitySold, decimal NewCurrentStock)> soldItems);
        Task BroadcastStockUpdateAsync(int productId, decimal? currentStock = null);
    }
}
