namespace InventarioApp.Models;

public class LoginRequest
{
    public string NombreUsuario { get; set; } = "";
    public string Password { get; set; } = "";
}

public class RegistroRequest
{
    public string NombreUsuario { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Email { get; set; }
    // No se envía Rol: la API siempre crea el registro público con rol Cliente.
}

public class AuthResponse
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = "";
    public string Rol { get; set; } = "";
    public string Token { get; set; } = "";
    public DateTime Expira { get; set; }
}