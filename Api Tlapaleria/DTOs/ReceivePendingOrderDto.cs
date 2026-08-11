using System.ComponentModel.DataAnnotations;
using Api_Tlapaleria.Enums;

namespace Api_Tlapaleria.DTOs
{
    public class ReceivePendingOrderDto
    {
        [Required]
        // Enviará 2 para "Cancelado" (agotado) o 3 para "Completado" (llegó la mercancía)
        public PendingOrderStatus FinalStatus { get; set; }

        // ==========================================
        // CAMPOS OPCIONALES (Solo aplican si llegó)
        // ==========================================

        public decimal? ReceivedQuantity { get; set; }

        // Precios del Padre (pueden venir nulos si no cambiaron)
        public decimal? NewSupplierPrice { get; set; }
        public decimal? NewProfitMargin { get; set; }

        // Precios de los Hijos (Presentaciones)
        public List<PresentationPriceUpdateDto>? PresentationPrices { get; set; }
        //Posiblemente en algun momneto se use para guaradar el numero de factura en el que llego este producto 
        //[MaxLength(200)]
        //public string? MovementNotes { get; set; } // "Factura 123", "Remisión 45", etc.
    }

    public class PresentationPriceUpdateDto
    {
        public int PresentationId { get; set; }
        public decimal NewPrice { get; set; } // El nuevo precio de venta de esta presentación
    }
}