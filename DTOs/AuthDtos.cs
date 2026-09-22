namespace InventarioAPI.DTOs
{
    public class RegistroDto
    {
        public string NombreUsuario { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Rol { get; set; } = "Empleado"; // "Admin" o "Empleado"
    }

    public class LoginDto
    {
        public string NombreUsuario { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public int Id { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime Expira { get; set; }
    }
}