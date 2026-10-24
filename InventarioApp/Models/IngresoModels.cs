namespace InventarioApp.Models;

// ----- Proveedores -----

public class ProveedorDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? CUIT { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
}

public class ProveedorCreateDto
{
    public string Nombre { get; set; } = "";
    public string? CUIT { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
}

// ----- Lo que se envía a POST /api/ingresos -----

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

// ----- Lo que devuelve la API -----

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

// ----- Línea en edición en la pantalla de ingresos (solo vive en la app) -----

public class LineaIngreso
{
    public ProductoDto Producto { get; set; } = new();
    public int Cantidad { get; set; } = 1;
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal => Cantidad * PrecioUnitario;
}