using Microsoft.EntityFrameworkCore;
using InventarioAPI.Models;

namespace InventarioAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Proveedor> Proveedores { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Ingreso> Ingresos { get; set; }
        public DbSet<IngresoDetalle> IngresoDetalles { get; set; }
        public DbSet<Salida> Salidas { get; set; }
        public DbSet<SalidaDetalle> SalidaDetalles { get; set; }
        public DbSet<LogError> LogErrores { get; set; }
        public DbSet<Auditoria> Auditorias { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Roles (datos iniciales fijos)
            modelBuilder.Entity<Rol>().HasIndex(r => r.Nombre).IsUnique();
            modelBuilder.Entity<Rol>().HasData(
                new Rol { Id = 1, Nombre = NombresRol.Admin },
                new Rol { Id = 2, Nombre = NombresRol.Vendedor },
                new Rol { Id = 3, Nombre = NombresRol.Cliente }
            );

            // Usuario -> Rol
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Rol)
                .WithMany(r => r.Usuarios)
                .HasForeignKey(u => u.RolId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.NombreUsuario)
                .IsUnique();

            // Producto -> Categoria (obligatoria)
            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Categoria)
                .WithMany(c => c.Productos)
                .HasForeignKey(p => p.CategoriaId)
                .OnDelete(DeleteBehavior.Restrict); // no permite borrar una categoría con productos

            // Producto -> Proveedor (opcional)
            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Proveedor)
                .WithMany(pr => pr.Productos)
                .HasForeignKey(p => p.ProveedorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ingreso -> Proveedor / Usuario
            modelBuilder.Entity<Ingreso>()
                .HasOne(i => i.Proveedor)
                .WithMany(p => p.Ingresos)
                .HasForeignKey(i => i.ProveedorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ingreso>()
                .HasOne(i => i.Usuario)
                .WithMany()
                .HasForeignKey(i => i.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            // Salida -> Cliente / Usuario
            modelBuilder.Entity<Salida>()
                .HasOne(s => s.Cliente)
                .WithMany(c => c.Salidas)
                .HasForeignKey(s => s.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Salida>()
                .HasOne(s => s.Usuario)
                .WithMany()
                .HasForeignKey(s => s.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ingreso -> IngresoDetalle (cabecera-detalle: si se borra el ingreso, se borran sus líneas)
            modelBuilder.Entity<IngresoDetalle>()
                .HasOne(d => d.Ingreso)
                .WithMany(i => i.Detalles)
                .HasForeignKey(d => d.IngresoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<IngresoDetalle>()
                .HasOne(d => d.Producto)
                .WithMany(p => p.IngresoDetalles)
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            // Salida -> SalidaDetalle
            modelBuilder.Entity<SalidaDetalle>()
                .HasOne(d => d.Salida)
                .WithMany(s => s.Detalles)
                .HasForeignKey(d => d.SalidaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SalidaDetalle>()
                .HasOne(d => d.Producto)
                .WithMany(p => p.SalidaDetalles)
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Auditoria>()
               .HasOne(a => a.Usuario)
               .WithMany()
               .HasForeignKey(a => a.UsuarioId)
               .OnDelete(DeleteBehavior.Restrict);

            // Índices para consultar los logs por fecha rápidamente
            modelBuilder.Entity<LogError>().HasIndex(l => l.Fecha);
            modelBuilder.Entity<Auditoria>().HasIndex(a => a.Fecha);
        }
    }
}