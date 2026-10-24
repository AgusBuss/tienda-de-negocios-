namespace InventarioApp.Models;

public class UsuarioListadoDto
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = "";
    public string? Email { get; set; }
    public string Rol { get; set; } = "";
}

public class UsuarioAdminCreateDto
{
    public string NombreUsuario { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Email { get; set; }
    public string Rol { get; set; } = "Cliente";
}

public class CambiarRolDto
{
    public string Rol { get; set; } = "";
}

/// <summary>Roles válidos del sistema (los mismos que NombresRol en la API).</summary>
public static class RolesApp
{
    public static readonly string[] Todos = { "Admin", "Vendedor", "Cliente" };
}