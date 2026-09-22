using System.ComponentModel.DataAnnotations.Schema;

namespace InventarioAPI.Models
{
    public class IngresoDetalle
    {
        public int Id { get; set; }

        public int IngresoId { get; set; }
        public Ingreso? Ingreso { get; set; }

        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }

        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioUnitario { get; set; }
    }
}