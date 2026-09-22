using System.ComponentModel.DataAnnotations.Schema;

namespace InventarioAPI.Models
{
    public class SalidaDetalle
    {
        public int Id { get; set; }

        public int SalidaId { get; set; }
        public Salida? Salida { get; set; }

        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }

        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioUnitario { get; set; }
    }
}