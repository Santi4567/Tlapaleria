using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Data
{
    public class TlapaleriaContext : DbContext
    {
        public TlapaleriaContext(DbContextOptions<TlapaleriaContext> options) : base(options)
        {
        }

        // TUS TABLAS
        public DbSet<User> Users { get; set; }
        public DbSet<Rol> Roles { get; set; }       // <--- AGREGAR
        public DbSet<Permiso> Permisos { get; set; } // <--- AGREGAR

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configuración de idioma español
            modelBuilder.UseCollation("utf8mb4_spanish_ci");

            // Configurar que el nombre del permiso sea único (No queremos dos permisos llamados "add.users")
            modelBuilder.Entity<Permiso>()
                .HasIndex(p => p.NombreSistema)
                .IsUnique();

            // Configurar que el nombre del rol sea único
            modelBuilder.Entity<Rol>()
                .HasIndex(r => r.Nombre)
                .IsUnique();
        }
    }
}