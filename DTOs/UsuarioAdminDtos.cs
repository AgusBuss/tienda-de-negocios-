using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.DTOs
{
    public class UsuarioListadoDto
    {
        public int Id { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Rol { get; set; } = string.Empty;
    }

    public class UsuarioAdminCreateDto
    {
        [Required, MaxLength(50)]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Email { get; set; }

        [Required]
        public string Rol { get; set; } = string.Empty;
    }

    public class CambiarRolDto
    {
        [Required]
        public string Rol { get; set; } = string.Empty;
    }
}