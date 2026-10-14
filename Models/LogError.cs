using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.Models
{
    public class LogError
    {
        public int Id { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;

        [Required, MaxLength(500)]
        public string Mensaje { get; set; } = string.Empty;

        public string? StackTrace { get; set; }

        [MaxLength(300)]
        public string? Ruta { get; set; }

        [MaxLength(10)]
        public string? Metodo { get; set; }

        public int StatusCode { get; set; }

        // Usuario que hizo la petición (null si no estaba logueado)
        public int? UsuarioId { get; set; }
    }
}