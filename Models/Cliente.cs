using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.Models
{
    public class Cliente
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? DNI { get; set; }

        [MaxLength(30)]
        public string? Telefono { get; set; }

        [MaxLength(150)]
        public string? Email { get; set; }

        [MaxLength(200)]
        public string? Direccion { get; set; }

        // Cuenta de usuario asociada (null si el cliente lo cargó un vendedor en mostrador)
        public int? UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        public ICollection<Salida> Salidas { get; set; } = new List<Salida>();
    }
}