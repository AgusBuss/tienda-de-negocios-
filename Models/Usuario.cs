using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.Models
{
    public class Usuario
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Email { get; set; }

        [Required, MaxLength(20)]
        public string Rol { get; set; } = "Empleado"; // Admin o Empleado, por ejemplo
    }
}