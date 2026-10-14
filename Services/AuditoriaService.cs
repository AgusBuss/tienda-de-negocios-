using System.Security.Claims;
using InventarioAPI.Data;
using InventarioAPI.Models;

namespace InventarioAPI.Services
{
    public class AuditoriaService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditoriaService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // Registra una acción del usuario logueado (toma su id del token JWT)
        public async Task RegistrarAsync(string accion, string entidad, int? entidadId = null, string? detalle = null)
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(claim, out var usuarioId)) return; // sin usuario identificado no se registra

            await RegistrarAsync(usuarioId, accion, entidad, entidadId, detalle);
        }

        // Versión con usuario explícito (se usa en el login, donde todavía no hay token)
        public async Task RegistrarAsync(int usuarioId, string accion, string entidad, int? entidadId = null, string? detalle = null)
        {
            if (detalle != null && detalle.Length > 500) detalle = detalle[..500];

            _context.Auditorias.Add(new Auditoria
            {
                Fecha = DateTime.Now,
                UsuarioId = usuarioId,
                Accion = accion,
                Entidad = entidad,
                EntidadId = entidadId,
                Detalle = detalle
            });

            await _context.SaveChangesAsync();
        }
    }
}