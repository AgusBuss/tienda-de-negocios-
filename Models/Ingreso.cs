using System.ComponentModel.DataAnnotations.Schema;

namespace InventarioAPI.Models
{
    public class Ingreso
    {
        public int Id { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;

        public int ProveedorId { get; set; }
        public Proveedor? Proveedor { get; set; }

        public int UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        public ICollection<IngresoDetalle> Detalles { get; set; } = new List<IngresoDetalle>();
    }
}