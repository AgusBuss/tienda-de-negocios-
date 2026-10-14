namespace InventarioAPI.Models
{
    public class Salida
    {
        public int Id { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;

        public int ClienteId { get; set; }
        public Cliente? Cliente { get; set; }

        public int UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        public ICollection<SalidaDetalle> Detalles { get; set; } = new List<SalidaDetalle>();
        public ICollection<MovimientoStock> Movimientos { get; set; } = new List<MovimientoStock>();
    }
}