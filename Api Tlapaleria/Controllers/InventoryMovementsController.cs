using Api_Tlapaleria.Attributes;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Enums;
using Api_Tlapaleria.Extensions; // Agregado para GetUserId()
using Api_Tlapaleria.Models;
using Api_Tlapaleria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Tlapaleria.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InventoryMovementsController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;

        public InventoryMovementsController(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        [HttpPost]
        [RequierePermiso("add.inventorymovements")]
        public async Task<ActionResult<ApiResponse<InventoryMovement>>> CreateMovement([FromBody] CreateInventoryMovementDto datos)
        {
            // Extracción limpia del usuario
            int userIdToken = User.GetUserId();

            var movimiento = await _inventoryService.RegisterMovementAsync(datos, userIdToken);

            return Ok(ApiResponse<InventoryMovement>.Exito(movimiento, "Movimiento de inventario registrado y stock actualizado correctamente."));
        }

        [HttpGet("product/{productId}")]
        [Authorize]
        [RequierePermiso("view.inventorymovements")]
        public async Task<ActionResult<ApiResponse<PagedResponse<InventoryMovement>>>> GetByProduct(
            int productId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var historial = await _inventoryService.GetMovementsByProductIdAsync(productId, page, pageSize);

            return Ok(ApiResponse<PagedResponse<InventoryMovement>>.Exito(historial, "Kardex obtenido correctamente."));
        }

        // GET: api/inventorymovements
        [HttpGet]
        [Authorize]
        [RequierePermiso("view.inventorymovements")]
        public async Task<ActionResult<ApiResponse<PagedResponse<InventoryMovement>>>> GetAllMovements(
            // /api/inventorymovements? productId = 5 & startDate = 2026 - 07 - 31

            // 1. Recibe un entero (ID). Sirve para filtrar el historial de un solo producto. 
            // Al tener '?', es opcional. Si no se envía, trae de todos los productos.
            [FromQuery] int? productId = null,

            // 2. Recibe una fecha (ej. "2026-07-31"). Sirve para marcar el inicio de un rango de búsqueda.
            // Si se manda sola (sin endDate), el controlador la usará para buscar solo en ese día específico.
            [FromQuery] DateTime? startDate = null,

            // 3. Recibe una fecha. Sirve para marcar el límite final del rango de búsqueda.
            [FromQuery] DateTime? endDate = null,

            // 4. Recibe un número entero que C# mapea al Enum (1=Entrada, 2=Merma, 3=Ajuste+, 4=Ajuste-, 5=Venta, 6=Devolución).
            // Sirve para traer solo un tipo de operación, por ejemplo, ver puras mermas.
            [FromQuery] MovementType? movementType = null,

            // 5. Recibe un entero. Sirve para saber qué página de la tabla de resultados quiere ver el usuario.
            // Tiene un valor por defecto de 1.
            [FromQuery] int page = 1,

            // 6. Recibe un entero. Sirve para limitar cuántos registros devuelve la API de golpe.
            // Tiene un valor por defecto de 50. El controlador tiene un tope interno para no aceptar más de 100.
            [FromQuery] int pageSize = 50)
        {
            // Límite de seguridad
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            // TRUCO UX: Si el usuario manda solo "startDate" (un día específico), 
            // asumimos que quiere ver solo ese día, así que igualamos endDate a startDate.
            if (startDate.HasValue && !endDate.HasValue)
            {
                endDate = startDate;
            }

            var historial = await _inventoryService.GetMovementsAsync(productId, startDate, endDate, movementType, page, pageSize);

            return Ok(ApiResponse<PagedResponse<InventoryMovement>>.Exito(historial, "Reporte de inventario generado correctamente."));
        }
    }
}