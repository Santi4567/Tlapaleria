namespace Api_Tlapaleria.DTOs
{
    public class PagedResponse<T>
    {
        public List<T> Data { get; set; } = new List<T>();
        public int TotalItems { get; set; }  // Cuántos productos hay en total en la BD
        public int TotalPages { get; set; }  // Cuántas páginas salen en total
        public int CurrentPage { get; set; } // En qué página estamos
    }
}
