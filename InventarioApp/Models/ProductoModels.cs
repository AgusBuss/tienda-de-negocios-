namespace InventarioApp.Models;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int PaginaActual { get; set; }
    public int TamanioPagina { get; set; }
    public int TotalRegistros { get; set; }
    public int TotalPaginas { get; set; }
}

public class ProductoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public string? ImagenUrl { get; set; }
    public int CategoriaId { get; set; }
    public string? CategoriaNombre { get; set; }
    public int? ProveedorId { get; set; }
    public string? ProveedorNombre { get; set; }
    public bool Activo { get; set; } = true;

    // Ayudas para la UI del catálogo
    public bool SinStock => Stock <= 0;
    public bool UltimasUnidades => Stock > 0 && Stock <= 5;

    /// <summary>Copia los datos editables, para precargar el formulario de edición.</summary>
    public ProductoCreateDto ComoCreateDto() => new()
    {
        Nombre = Nombre,
        Descripcion = Descripcion,
        Precio = Precio,
        Stock = Stock,
        CategoriaId = CategoriaId,
        ProveedorId = ProveedorId,
        Activo = Activo
    };
}

public class ProductoCreateDto
{
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public int CategoriaId { get; set; }
    public int? ProveedorId { get; set; }
    public bool Activo { get; set; } = true;
}

public class CategoriaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
}

public class CategoriaCreateDto
{
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
}

/// <summary>Filtros del catálogo; se convierten en query string para GET /api/productos.</summary>
public class FiltroProductos
{
    public int Pagina { get; set; } = 1;
    public int TamanioPagina { get; set; } = 10;
    public int? CategoriaId { get; set; }
    public string? Buscar { get; set; }
    public bool SoloDisponibles { get; set; }
    public decimal? PrecioMin { get; set; }
    public decimal? PrecioMax { get; set; }
    public string Orden { get; set; } = "nombre"; // nombre | precio_asc | precio_desc | recientes
    public bool IncluirInactivos { get; set; }
}