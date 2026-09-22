using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.Models
{
    public class Categoria
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Descripcion { get; set; }

        // Relación: una categoría tiene muchos productos
        public ICollection<Producto> Productos { get; set; } = new List<Producto>();
    }
}