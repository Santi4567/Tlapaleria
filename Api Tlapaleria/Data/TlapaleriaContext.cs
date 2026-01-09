using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Data
{
    public class TlapaleriaContext : DbContext
    {
        public TlapaleriaContext(DbContextOptions<TlapaleriaContext> options) : base(options)
        {
        }

        // TUS TABLAS(Carpeta de Models)
        public DbSet<User> Users { get; set; }//user tabla
        public DbSet<Rol> Roles { get; set; }       // <--- rol tabla
        public DbSet<Permiso> Permisos { get; set; } // <--- Permisos tabla

        public DbSet<Supplier> Suppliers { get; set; } // suppliers tabla

        public DbSet<Product> Products { get; set; }  // <--- Esto le faltaba
        public DbSet<ProductPresentation> ProductPresentations { get; set; } // <--- Y esto

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // Buena práctica mantenerlo
            modelBuilder.UseCollation("utf8mb4_spanish_ci");

            // Índices (Igual que antes)
            modelBuilder.Entity<Permiso>().HasIndex(p => p.NombreSistema).IsUnique();
            modelBuilder.Entity<Rol>().HasIndex(r => r.Nombre).IsUnique();

            // --- AQUÍ ESTÁ EL ARREGLO para traer correctamente los roles ---
            modelBuilder.Entity<Rol>()
                .HasMany(r => r.Permisos)
                .WithMany(p => p.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "RolPermiso", // 1. Nombre exacto de la tabla intermedia en MySQL

                    // 2. Configuración del lado de "Permisos" (Right Key)
                    j => j.HasOne<Permiso>()
                          .WithMany()
                          .HasForeignKey("PermisoId"), // <--- AQUÍ LE DECIMOS: "Usa PermisoId (singular)"

                    // 3. Configuración del lado de "Roles" (Left Key)
                    j => j.HasOne<Rol>()
                          .WithMany()
                          .HasForeignKey("RolId"),     // <--- AQUÍ LE DECIMOS: "Usa RolId"

                    // 4. Configuración final de la tabla
                    j => j.ToTable("RolPermiso")
                );

        }
    }
}