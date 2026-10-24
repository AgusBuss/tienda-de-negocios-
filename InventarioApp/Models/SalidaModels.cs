namespace InventarioApp.Models;

// ----- Clientes -----

public class ClienteDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? DNI { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
}

public class ClienteCreateDto
{
    public string Nombre { get; set; } = "";
    public string? DNI { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
}

// ----- Lo que se envía a POST /api/salidas -----

public class SalidaDetalleCreateDto
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; } // la API lo ignora y usa Producto.Precio
}

public class SalidaCreateDto
{
    public int ClienteId { get; set; }
    public List<SalidaDetalleCreateDto> Detalles { get; set; } = new();
}

// ----- Lo que devuelve la API -----

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

// ----- Línea en edición en la pantalla de ventas (solo vive en la app) -----

public class LineaVenta
{
    public ProductoDto Producto { get; set; } = new();
    public int Cantidad { get; set; } = 1;
    public decimal Subtotal => Cantidad * Producto.Precio; // referencia: el precio final lo define el servidor
}