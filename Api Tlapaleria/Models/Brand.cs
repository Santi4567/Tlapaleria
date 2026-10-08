namespace Api_Tlapaleria.Models
{
    // Catálogo de marcas. Sirve para sugerir y normalizar el texto;
    // Products.Brand sigue siendo un string (sin relación/FK).
    public class Brand
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public bool IsActive { get; set; } = true;
    }
}