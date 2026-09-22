using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.Models
{
    public class Proveedor
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? CUIT { get; set; }

        [MaxLength(30)]
        public string? Telefono { get; set; }

        [MaxLength(150)]
        public string? Email { get; set; }

        [MaxLength(200)]
        public string? Direccion { get; set; }

        public ICollection<Producto> Productos { get; set; } = new List<Producto>();
        public ICollection<Ingreso> Ingresos { get; set; } = new List<Ingreso>();
    }
}