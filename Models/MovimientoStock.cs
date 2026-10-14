using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.Models
{
    public enum TipoMovimiento
    {
        Entrada = 1,   // ingreso de mercadería (suma stock)
        Salida = 2,    // venta (resta stock)
        Ajuste = 3     // corrección manual
    }

    public class MovimientoStock
    {
        public int Id { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;

        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }

        public TipoMovimiento Tipo { get; set; }

        // Positiva si suma stock, negativa si resta
        public int Cantidad { get; set; }

        public int UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        // Documento que originó el movimiento (uno u otro, o ninguno en un ajuste)
        public int? IngresoId { get; set; }
        public Ingreso? Ingreso { get; set; }

        public int? SalidaId { get; set; }
        public Salida? Salida { get; set; }

        [MaxLength(200)]
        public string? Observacion { get; set; }
    }
}