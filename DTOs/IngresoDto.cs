namespace InventarioAPI.DTOs
{
    public class IngresoDetalleCreateDto
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
    }

    public class IngresoCreateDto
    {
        public int ProveedorId { get; set; }
        public List<IngresoDetalleCreateDto> Detalles { get; set; } = new();
    }

    public class IngresoDetalleDto
    {
        public int ProductoId { get; set; }
        public string? ProductoNombre { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal => Cantidad * PrecioUnitario;
    }

    public class IngresoDto
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public int ProveedorId { get; set; }
        public string? ProveedorNombre { get; set; }
        public int UsuarioId { get; set; }
        public string? UsuarioNombre { get; set; }
        public decimal Total { get; set; }
        public List<IngresoDetalleDto> Detalles { get; set; } = new();
    }
}