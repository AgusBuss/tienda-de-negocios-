using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.DTOs
{
    // Una línea del carrito: el precio NO viaja, lo define el servidor
    public class CompraItemDto
    {
        [Required]
        public int ProductoId { get; set; }

        [Range(1, 1000, ErrorMessage = "La cantidad debe estar entre 1 y 1000.")]
        public int Cantidad { get; set; }
    }

    public class CompraCreateDto
    {
        [Required, MinLength(1, ErrorMessage = "El carrito está vacío.")]
        public List<CompraItemDto> Items { get; set; } = new();

        // "Tarjeta", "Transferencia" o "Efectivo"
        [Required]
        public string MetodoPago { get; set; } = string.Empty;

        // Datos de envío / facturación
        [Required, MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? DNI { get; set; }

        [MaxLength(30)]
        public string? Telefono { get; set; }

        [Required, MaxLength(200)]
        public string Direccion { get; set; } = string.Empty;

        // Solo si el método es Tarjeta. Se validan y se descartan (nunca se guardan).
        [MaxLength(19)]
        public string? NumeroTarjeta { get; set; }

        [MaxLength(4)]
        public string? Cvv { get; set; }
    }

    public class CompraItemResultadoDto
    {
        public int ProductoId { get; set; }
        public string? ProductoNombre { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class CompraResultadoDto
    {
        public int VentaId { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Total { get; set; }
        public string? MetodoPago { get; set; }
        public string? Referencia { get; set; }
        public string? UltimosDigitos { get; set; }
        public List<CompraItemResultadoDto> Items { get; set; } = new();
    }
}