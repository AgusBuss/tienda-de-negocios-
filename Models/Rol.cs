using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.Models
{
    public class Rol
    {
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string Nombre { get; set; } = string.Empty;

        public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    }

    // Constantes para no escribir strings sueltos por todo el código
    public static class NombresRol
    {
        public const string Admin = "Admin";
        public const string Vendedor = "Vendedor";
        public const string Cliente = "Cliente";
        public const string AdminVendedor = "Admin,Vendedor";
    }
}