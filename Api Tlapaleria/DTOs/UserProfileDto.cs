namespace Api_Tlapaleria.DTOs
{
    public class UserProfileDto
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Name { get; set; }
        public string Rol { get; set; }

        // Aquí está la magia del agrupamiento:
        // Clave: "users", Valor: ["add.users", "edit.users"]
        public Dictionary<string, List<string>> Permisos { get; set; }
    }
}