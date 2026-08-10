namespace Api_Tlapaleria.Enums
{
    public enum PendingOrderStatus
    {
        /// <summary>
        /// 0 = Solo es una nota en la libreta. (Falta mercancía).
        /// </summary>
        Pendiente = 0,

        /// <summary>
        /// 1 = Ya se le mandó el mensaje o la orden de compra al proveedor.
        /// </summary>
        Pedido = 1,

        /// <summary>
        /// 2 = Ya no se necesita o el proveedor indicó que está agotado.
        /// </summary>
        Cancelado = 2,

        /// <summary>
        /// 3 = Ya llegó la mercancía, se costeo y entró al stock.
        /// </summary>
        Completado = 3
    }
}