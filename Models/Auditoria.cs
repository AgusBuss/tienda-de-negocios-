using System.ComponentModel.DataAnnotations;

namespace InventarioAPI.Models
{
    public class Auditoria
    {
        public int Id { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;

        // Quién lo hizo
        public int UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        // Qué hizo: Crear, Modificar, Eliminar, Login, etc.
        [Required, MaxLength(30)]
        public string Accion { get; set; } = string.Empty;

        // Sobre qué tabla: Producto, Venta, Ingreso, etc.
        [Required, MaxLength(50)]
        public string Entidad { get; set; } = string.Empty;

        // Id del registro afectado (si corresponde)
        public int? EntidadId { get; set; }

        [MaxLength(500)]
        public string? Detalle { get; set; }
    }
}