using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Extensions;
using Api_Tlapaleria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api_Tlapaleria.Hubs
{
    [Authorize]
    public class InventoryHub : Hub
    {
        private readonly IStockReservationService _stockReservationService;

        public InventoryHub(IStockReservationService stockReservationService)
        {
            _stockReservationService = stockReservationService;
        }

        // Apartar o actualizar la cantidad de una presentación en la lista de venta activa de esta caja
        public async Task<ApiResponse<StockReservationResultDto>> SetItemQuantity(int presentationId, decimal quantity)
        {
            int userId = Context.User!.GetUserId();
            var resultado = await _stockReservationService.SetItemReservationAsync(Context.ConnectionId, userId, presentationId, quantity);

            if (!resultado.Success)
            {
                return new ApiResponse<StockReservationResultDto>
                {
                    Success = false,
                    Message = resultado.Message,
                    Data = resultado
                };
            }

            return ApiResponse<StockReservationResultDto>.Exito(resultado, resultado.Message);
        }

        // Quitar una presentación de la lista de venta activa
        public async Task<ApiResponse<bool>> RemoveItem(int presentationId)
        {
            await _stockReservationService.RemoveItemReservationAsync(Context.ConnectionId, presentationId);
            return ApiResponse<bool>.Exito(true, "Producto liberado del carrito.");
        }

        // Limpiar toda la lista de venta de esta caja
        public async Task<ApiResponse<bool>> ClearCart()
        {
            await _stockReservationService.ClearCartAsync(Context.ConnectionId);
            return ApiResponse<bool>.Exito(true, "Carrito liberado.");
        }

        // Si la caja se desconecta o cierra la aplicación, devuelve su stock apartado al anaquel automáticamente
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await _stockReservationService.ClearCartAsync(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
