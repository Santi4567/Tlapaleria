using Api_Tlapaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Data
{
    public class TlapaleriaContext : DbContext
    {
        public TlapaleriaContext(DbContextOptions<TlapaleriaContext> options) : base(options)
        {
        }

        //TABLAS/Modelos (Carpeta de /Models)
        public DbSet<User> Users { get; set; }//<--- Modelo de la tabla de Usuarios 
        public DbSet<Rol> Roles { get; set; }       // <--- Modelo de la tabla de roles 
        public DbSet<Permiso> Permisos { get; set; } // <--- Modelo de la tabla de permisos 

        public DbSet<Supplier> Suppliers { get; set; } //<--- Modelo de la tabla de proveedores 

        public DbSet<Product> Products { get; set; }  //<--- Modelo de la tabla de productos 
        public DbSet<ProductPresentation> ProductPresentations { get; set; } //<--- Modelo la tabla de presentacion del producto

        protected override void OnModelCreating(ModelBuilder modelBuilder) // <--- Configuracion necesaria para mostrar bien los permisos de los usuarios 
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.UseCollation("utf8mb4_spanish_ci");

            // Índices 
            modelBuilder.Entity<Permiso>().HasIndex(p => p.NombreSistema).IsUnique();
            modelBuilder.Entity<Rol>().HasIndex(r => r.Nombre).IsUnique();

            // --- ARREGLO para traer correctamente los roles ---
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

        public DbSet<PendingOrder> PendingOrders { get; set; } //<--- Modelo de la tabla de pedidos 

        public DbSet<InventoryMovement> InventoryMovements { get; set; } //<--- Modelo de la tabla de Kardex
    }
}