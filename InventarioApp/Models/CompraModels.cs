namespace InventarioApp.Models;

// ----- Lo que se envía a POST /api/compras -----

public class CompraItemDto
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
}

public class CompraCreateDto
{
    public List<CompraItemDto> Items { get; set; } = new();

    // "Tarjeta", "Transferencia" o "Efectivo"
    public string MetodoPago { get; set; } = "";

    // Datos de envío / facturación
    public string Nombre { get; set; } = "";
    public string? DNI { get; set; }
    public string? Telefono { get; set; }
    public string Direccion { get; set; } = "";

    // Solo si el método es Tarjeta
    public string? NumeroTarjeta { get; set; }
    public string? Cvv { get; set; }
}

// ----- Lo que devuelve la API -----

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

// ----- Carrito en memoria (solo vive en el dispositivo) -----

public class CarritoItem
{
    public ProductoDto Producto { get; set; } = new();
    public int Cantidad { get; set; }

    // Solo para mostrar: el precio definitivo lo calcula el servidor al comprar.
    public decimal Subtotal => Producto.Precio * Cantidad;
}