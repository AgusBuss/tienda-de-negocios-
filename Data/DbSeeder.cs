using Microsoft.EntityFrameworkCore;
using InventarioAPI.Models;

namespace InventarioAPI.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            var hayAdmin = await context.Usuarios.AnyAsync(u => u.Rol!.Nombre == NombresRol.Admin);
            if (hayAdmin) return;

            var rolAdmin = await context.Roles.FirstAsync(r => r.Nombre == NombresRol.Admin);

            context.Usuarios.Add(new Usuario
            {
                NombreUsuario = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                Email = "admin@tienda.com",
                RolId = rolAdmin.Id
            });

            await context.SaveChangesAsync();
        }
    }
}