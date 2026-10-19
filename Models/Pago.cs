using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.Models
{
    public enum MetodoPago
    {
        Tarjeta = 1,
        Transferencia = 2,
        Efectivo = 3
    }

    public class Pago
    {
        public int Id { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;

        // Cada pago corresponde a exactamente una venta (relación 1 a 1)
        public int SalidaId { get; set; }
        public Salida? Salida { get; set; }

        public MetodoPago Metodo { get; set; }

        // Código de operación generado por la simulación
        [Required, MaxLength(40)]
        public string Referencia { get; set; } = string.Empty;

        // Solo para pagos con tarjeta
        [MaxLength(4)]
        public string? UltimosDigitos { get; set; }
    }
}