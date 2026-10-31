using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Hubs;
using Api_Tlapaleria.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class StockReservationService : IStockReservationService
    {
        private sealed class CartItemReservation
        {
            public string ConnectionId { get; set; } = string.Empty;
            public int UserId { get; set; }
            public int ProductId { get; set; }
            public int PresentationId { get; set; }
            public decimal Quantity { get; set; }
            public decimal StockFactor { get; set; }
            public decimal BaseQuantity { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<InventoryHub> _hubContext;
        private readonly object _syncLock = new();

        // ConnectionId -> (PresentationId -> Reserva)
        private readonly Dictionary<string, Dictionary<int, CartItemReservation>> _cartsByConnection = new();

        public StockReservationService(IServiceScopeFactory scopeFactory, IHubContext<InventoryHub> hubContext)
        {
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
        }

        public async Task<StockReservationResultDto> SetItemReservationAsync(string connectionId, int userId, int presentationId, decimal quantity)
        {
            if (quantity <= 0)
            {
                await RemoveItemReservationAsync(connectionId, presentationId);
                return new StockReservationResultDto
                {
                    Success = true,
                    Message = "Producto retirado del apartado.",
                    PresentationId = presentationId,
                    RequestedQuantity = 0
                };
            }

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TlapaleriaContext>();

            var presentacion = await context.ProductPresentations
                .AsNoTracking()
                .Include(p => p.Product)
                .FirstOrDefaultAsync(p => p.Id == presentationId);

            if (presentacion == null || !presentacion.IsActive || presentacion.Product == null || !presentacion.Product.IsActive)
            {
                return new StockReservationResultDto
                {
                    Success = false,
                    Message = "La presentación o el producto no existe o se encuentra inactivo.",
                    PresentationId = presentationId,
                    RequestedQuantity = quantity
                };
            }

            var producto = presentacion.Product;

            if (!producto.AllowFractions && (quantity % 1 != 0))
            {
                return new StockReservationResultDto
                {
                    Success = false,
                    Message = $"El producto '{producto.Name}' solo permite unidades enteras ({producto.UnitOfMeasure}).",
                    ProductId = producto.Id,
                    PresentationId = presentationId,
                    RequestedQuantity = quantity,
                    CurrentStock = producto.CurrentStock
                };
            }

            if (!producto.IsInventoryTracked)
            {
                return new StockReservationResultDto
                {
                    Success = true,
                    Message = "Producto sin rastreo de inventario.",
                    ProductId = producto.Id,
                    PresentationId = presentationId,
                    RequestedQuantity = quantity,
                    CurrentStock = producto.CurrentStock,
                    ReservedStock = 0,
                    AvailableStock = producto.CurrentStock
                };
            }

            decimal baseQuantity = quantity * presentacion.StockFactor;
            StockUpdateNotificationDto notification;
            StockReservationResultDto result;

            lock (_syncLock)
            {
                decimal reservedExcludingThisItem = GetReservedStockExcludingItemUnsafe(producto.Id, connectionId, presentationId);
                decimal availableForThisItem = Math.Max(0, producto.CurrentStock - reservedExcludingThisItem);

                if (baseQuantity > availableForThisItem)
                {
                    decimal currentTotalReserved = GetReservedStockUnsafe(producto.Id);
                    decimal currentAvailable = Math.Max(0, producto.CurrentStock - currentTotalReserved);

                    return new StockReservationResultDto
                    {
                        Success = false,
                        Message = $"Stock insuficiente en anaquel para '{producto.Name}'. Disponible: {availableForThisItem:0.###} {producto.UnitOfMeasure} (Apartado en cajas: {reservedExcludingThisItem:0.###}).",
                        ProductId = producto.Id,
                        PresentationId = presentationId,
                        RequestedQuantity = quantity,
                        CurrentStock = producto.CurrentStock,
                        ReservedStock = currentTotalReserved,
                        AvailableStock = currentAvailable
                    };
                }

                if (!_cartsByConnection.TryGetValue(connectionId, out var cart))
                {
                    cart = new Dictionary<int, CartItemReservation>();
                    _cartsByConnection[connectionId] = cart;
                }

                cart[presentationId] = new CartItemReservation
                {
                    ConnectionId = connectionId,
                    UserId = userId,
                    ProductId = producto.Id,
                    PresentationId = presentationId,
                    Quantity = quantity,
                    StockFactor = presentacion.StockFactor,
                    BaseQuantity = baseQuantity,
                    UpdatedAt = DateTime.Now
                };

                decimal totalReserved = GetReservedStockUnsafe(producto.Id);
                decimal availableStock = Math.Max(0, producto.CurrentStock - totalReserved);

                notification = new StockUpdateNotificationDto
                {
                    ProductId = producto.Id,
                    CurrentStock = producto.CurrentStock,
                    ReservedStock = totalReserved,
                    AvailableStock = availableStock
                };

                result = new StockReservationResultDto
                {
                    Success = true,
                    Message = "Stock apartado correctamente.",
                    ProductId = producto.Id,
                    PresentationId = presentationId,
                    RequestedQuantity = quantity,
                    CurrentStock = producto.CurrentStock,
                    ReservedStock = totalReserved,
                    AvailableStock = availableStock
                };
            }

            await _hubContext.Clients.All.SendAsync("StockUpdated", notification);
            return result;
        }

        public async Task RemoveItemReservationAsync(string connectionId, int presentationId)
        {
            int? affectedProductId = null;

            lock (_syncLock)
            {
                if (_cartsByConnection.TryGetValue(connectionId, out var cart) &&
                    cart.TryGetValue(presentationId, out var removed))
                {
                    affectedProductId = removed.ProductId;
                    cart.Remove(presentationId);

                    if (cart.Count == 0)
                        _cartsByConnection.Remove(connectionId);
                }
            }

            if (affectedProductId.HasValue)
            {
                await BroadcastStockUpdateAsync(affectedProductId.Value);
            }
        }

        public async Task ClearCartAsync(string connectionId)
        {
            List<int> affectedProductIds;

            lock (_syncLock)
            {
                if (!_cartsByConnection.TryGetValue(connectionId, out var cart))
                    return;

                affectedProductIds = cart.Values.Select(i => i.ProductId).Distinct().ToList();
                _cartsByConnection.Remove(connectionId);
            }

            foreach (var productId in affectedProductIds)
            {
                await BroadcastStockUpdateAsync(productId);
            }
        }

        public decimal GetReservedStock(int productId)
        {
            lock (_syncLock)
            {
                return GetReservedStockUnsafe(productId);
            }
        }

        public decimal GetReservedStockExcluding(int productId, int userId, string? connectionId = null)
        {
            lock (_syncLock)
            {
                decimal total = 0;

                foreach (var (connId, cart) in _cartsByConnection)
                {
                    if (!string.IsNullOrWhiteSpace(connectionId))
                    {
                        if (connId == connectionId)
                            continue;
                    }
                    else if (cart.Values.Any(i => i.UserId == userId))
                    {
                        continue;
                    }

                    foreach (var item in cart.Values)
                    {
                        if (item.ProductId == productId)
                            total += item.BaseQuantity;
                    }
                }

                return total;
            }
        }

        public void PopulateReservedStock(Product product)
        {
            if (product == null) return;

            if (!product.IsInventoryTracked)
            {
                product.ReservedStock = 0;
                return;
            }

            product.ReservedStock = GetReservedStock(product.Id);
        }

        public void PopulateReservedStock(IEnumerable<Product> products)
        {
            if (products == null) return;

            lock (_syncLock)
            {
                foreach (var product in products)
                {
                    product.ReservedStock = product.IsInventoryTracked
                        ? GetReservedStockUnsafe(product.Id)
                        : 0;
                }
            }
        }

        public async Task ConsumeReservationsOnSaleAsync(
            int userId,
            string? connectionId,
            IEnumerable<(int ProductId, int PresentationId, decimal QuantitySold, decimal NewCurrentStock)> soldItems)
        {
            var itemsList = soldItems.ToList();
            var notifications = new List<StockUpdateNotificationDto>();

            lock (_syncLock)
            {
                var targetConnections = !string.IsNullOrWhiteSpace(connectionId) && _cartsByConnection.ContainsKey(connectionId)
                    ? new List<string> { connectionId }
                    : _cartsByConnection
                        .Where(kvp => kvp.Value.Values.Any(i => i.UserId == userId))
                        .Select(kvp => kvp.Key)
                        .ToList();

                foreach (var sold in itemsList)
                {
                    decimal remainingToDeduct = sold.QuantitySold;

                    foreach (var connId in targetConnections)
                    {
                        if (remainingToDeduct <= 0) break;
                        if (!_cartsByConnection.TryGetValue(connId, out var cart)) continue;

                        if (cart.TryGetValue(sold.PresentationId, out var reservation))
                        {
                            if (reservation.Quantity <= remainingToDeduct)
                            {
                                remainingToDeduct -= reservation.Quantity;
                                cart.Remove(sold.PresentationId);
                            }
                            else
                            {
                                reservation.Quantity -= remainingToDeduct;
                                reservation.BaseQuantity = reservation.Quantity * reservation.StockFactor;
                                remainingToDeduct = 0;
                            }

                            if (cart.Count == 0)
                                _cartsByConnection.Remove(connId);
                        }
                    }
                }

                foreach (var group in itemsList.GroupBy(i => i.ProductId))
                {
                    int productId = group.Key;
                    decimal newCurrentStock = group.Last().NewCurrentStock;
                    decimal reserved = GetReservedStockUnsafe(productId);

                    notifications.Add(new StockUpdateNotificationDto
                    {
                        ProductId = productId,
                        CurrentStock = newCurrentStock,
                        ReservedStock = reserved,
                        AvailableStock = Math.Max(0, newCurrentStock - reserved)
                    });
                }
            }

            foreach (var notification in notifications)
            {
                await _hubContext.Clients.All.SendAsync("StockUpdated", notification);
            }
        }

        public async Task BroadcastStockUpdateAsync(int productId, decimal? currentStock = null)
        {
            decimal physicalStock;

            if (currentStock.HasValue)
            {
                physicalStock = currentStock.Value;
            }
            else
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TlapaleriaContext>();

                var prod = await context.Products
                    .AsNoTracking()
                    .Where(p => p.Id == productId)
                    .Select(p => new { p.CurrentStock, p.IsInventoryTracked })
                    .FirstOrDefaultAsync();

                if (prod == null || !prod.IsInventoryTracked)
                    return;

                physicalStock = prod.CurrentStock;
            }

            decimal reservedStock = GetReservedStock(productId);
            var notification = new StockUpdateNotificationDto
            {
                ProductId = productId,
                CurrentStock = physicalStock,
                ReservedStock = reservedStock,
                AvailableStock = Math.Max(0, physicalStock - reservedStock)
            };

            await _hubContext.Clients.All.SendAsync("StockUpdated", notification);
        }

        private decimal GetReservedStockUnsafe(int productId)
        {
            decimal total = 0;
            foreach (var cart in _cartsByConnection.Values)
            {
                foreach (var item in cart.Values)
                {
                    if (item.ProductId == productId)
                        total += item.BaseQuantity;
                }
            }
            return total;
        }

        private decimal GetReservedStockExcludingItemUnsafe(int productId, string connectionId, int presentationId)
        {
            decimal total = 0;
            foreach (var (connId, cart) in _cartsByConnection)
            {
                foreach (var (presId, item) in cart)
                {
                    if (connId == connectionId && presId == presentationId)
                        continue;

                    if (item.ProductId == productId)
                        total += item.BaseQuantity;
                }
            }
            return total;
        }
    }
}
