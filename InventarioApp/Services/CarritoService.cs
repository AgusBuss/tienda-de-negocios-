using InventarioApp.Models;

namespace InventarioApp.Services;

/// <summary>
/// Carrito de compras en memoria del dispositivo.
/// Vive mientras la app está abierta; el precio definitivo lo calcula el servidor al comprar.
/// </summary>
public class CarritoService
{
    private readonly List<CarritoItem> _items = new();

    public IReadOnlyList<CarritoItem> Items => _items;
    public int CantidadTotal => _items.Sum(i => i.Cantidad);
    public decimal Total => _items.Sum(i => i.Subtotal);
    public bool EstaVacio => _items.Count == 0;

    /// <summary>Se dispara cuando el carrito cambia, para refrescar el contador del menú.</summary>
    public event Action? Cambio;

    /// <summary>Agrega una unidad (o la cantidad indicada) sin superar el stock. Devuelve un mensaje si no pudo.</summary>
    public string? Agregar(ProductoDto producto, int cantidad = 1)
    {
        if (producto.SinStock)
            return "Este producto no tiene stock.";

        var existente = _items.FirstOrDefault(i => i.Producto.Id == producto.Id);
        var nueva = (existente?.Cantidad ?? 0) + cantidad;

        if (nueva > producto.Stock)
            return $"Solo quedan {producto.Stock} unidad(es) de este producto.";

        if (existente is null)
            _items.Add(new CarritoItem { Producto = producto, Cantidad = cantidad });
        else
        {
            existente.Cantidad = nueva;
            existente.Producto = producto; // refresca precio/stock con lo último que vimos
        }

        Cambio?.Invoke();
        return null;
    }

    /// <summary>Cambia la cantidad de una línea. Si llega a 0 o menos, la quita.</summary>
    public string? CambiarCantidad(int productoId, int cantidad)
    {
        var item = _items.FirstOrDefault(i => i.Producto.Id == productoId);
        if (item is null) return null;

        if (cantidad <= 0)
        {
            _items.Remove(item);
            Cambio?.Invoke();
            return null;
        }

        if (cantidad > item.Producto.Stock)
            return $"Solo quedan {item.Producto.Stock} unidad(es) de este producto.";

        item.Cantidad = cantidad;
        Cambio?.Invoke();
        return null;
    }

    public void Quitar(int productoId)
    {
        _items.RemoveAll(i => i.Producto.Id == productoId);
        Cambio?.Invoke();
    }

    public void Vaciar()
    {
        _items.Clear();
        Cambio?.Invoke();
    }

    /// <summary>Convierte el carrito en las líneas que espera POST /api/compras (sin precios).</summary>
    public List<CompraItemDto> ComoItemsDeCompra() =>
        _items.Select(i => new CompraItemDto { ProductoId = i.Producto.Id, Cantidad = i.Cantidad }).ToList();
}