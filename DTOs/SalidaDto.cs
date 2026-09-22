namespace InventarioAPI.DTOs
{
    public class SalidaDetalleCreateDto
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
    }

    public class SalidaCreateDto
    {
        public int ClienteId { get; set; }
        public List<SalidaDetalleCreateDto> Detalles { get; set; } = new();
    }

    public class SalidaDetalleDto
    {
        public int ProductoId { get; set; }
        public string? ProductoNombre { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal => Cantidad * PrecioUnitario;
    }

    public class SalidaDto
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public int ClienteId { get; set; }
        public string? ClienteNombre { get; set; }
        public int UsuarioId { get; set; }
        public string? UsuarioNombre { get; set; }
        public decimal Total { get; set; }
        public List<SalidaDetalleDto> Detalles { get; set; } = new();
    }
}