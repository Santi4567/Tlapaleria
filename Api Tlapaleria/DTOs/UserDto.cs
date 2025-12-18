namespace Api_Tlapaleria.DTOs
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public string Rol { get; set; } // Devolvemos el nombre ("Vendedor"), no el ID
        public bool IsActive { get; set; }
    }
}