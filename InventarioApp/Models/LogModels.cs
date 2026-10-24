namespace InventarioApp.Models;

public class LogErrorDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string Mensaje { get; set; } = "";
    public string? StackTrace { get; set; }
    public string? Ruta { get; set; }
    public string? Metodo { get; set; }
    public int StatusCode { get; set; }
    public int? UsuarioId { get; set; }
}

public class AuditoriaDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public int UsuarioId { get; set; }
    public string? UsuarioNombre { get; set; }
    public string Accion { get; set; } = "";
    public string Entidad { get; set; } = "";
    public int? EntidadId { get; set; }
    public string? Detalle { get; set; }
}