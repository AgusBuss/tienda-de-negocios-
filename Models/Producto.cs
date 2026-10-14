using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventarioAPI.Models
{
    public class Producto
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Descripcion { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Precio { get; set; }

        // Stock actual. Se actualiza con cada MovimientoStock, que guarda el historial.
        public int Stock { get; set; } = 0;

        [MaxLength(300)]
        public string? ImagenUrl { get; set; }

        // false = dado de baja: no se muestra en el catálogo ni se puede vender
        public bool Activo { get; set; } = true;

        // FK Categoria (obligatoria)
        public int CategoriaId { get; set; }
        public Categoria? Categoria { get; set; }

        // FK Proveedor (opcional: el "proveedor habitual")
        public int? ProveedorId { get; set; }
        public Proveedor? Proveedor { get; set; }

        public ICollection<IngresoDetalle> IngresoDetalles { get; set; } = new List<IngresoDetalle>();
        public ICollection<SalidaDetalle> SalidaDetalles { get; set; } = new List<SalidaDetalle>();
        public ICollection<MovimientoStock> Movimientos { get; set; } = new List<MovimientoStock>();
    }
}