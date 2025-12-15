using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Data
{
    public class TlapaleriaContext : DbContext
    {
        // Constructor: Recibe las opciones (como la cadena de conexión) y las pasa a la clase base
        public TlapaleriaContext(DbContextOptions<TlapaleriaContext> options) : base(options)
        {
        }

        // Aquí registras tus tablas. 
        // El nombre de la propiedad "Users" será el nombre de la tabla en MySQL.
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configuración global para usar la collation en español que hablamos antes
            modelBuilder.UseCollation("utf8mb4_spanish_ci");
        }
    }
}