using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioAPI.Data;
using InventarioAPI.DTOs;
using InventarioAPI.Models;

namespace InventarioAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = NombresRol.Admin)]
    public class LogsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LogsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/logs/errores?pagina=1&tamanioPagina=20&desde=2026-10-01&hasta=2026-10-31
        [HttpGet("errores")]
        public async Task<ActionResult<PagedResult<LogErrorDto>>> GetErrores(
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanioPagina = 20,
            [FromQuery] DateTime? desde = null,
            [FromQuery] DateTime? hasta = null)
        {
            if (pagina < 1) pagina = 1;
            if (tamanioPagina < 1 || tamanioPagina > 100) tamanioPagina = 20;

            var query = _context.LogErrores.AsNoTracking().AsQueryable();

            if (desde.HasValue) query = query.Where(l => l.Fecha >= desde.Value);
            if (hasta.HasValue) query = query.Where(l => l.Fecha < hasta.Value.Date.AddDays(1));

            query = query.OrderByDescending(l => l.Fecha);

            var totalRegistros = await query.CountAsync();

            var items = await query
                .Skip((pagina - 1) * tamanioPagina)
                .Take(tamanioPagina)
                .Select(l => new LogErrorDto
                {
                    Id = l.Id,
                    Fecha = l.Fecha,
                    Mensaje = l.Mensaje,
                    StackTrace = l.StackTrace,
                    Ruta = l.Ruta,
                    Metodo = l.Metodo,
                    StatusCode = l.StatusCode,
                    UsuarioId = l.UsuarioId
                })
                .ToListAsync();

            return Ok(new PagedResult<LogErrorDto>
            {
                Items = items,
                PaginaActual = pagina,
                TamanioPagina = tamanioPagina,
                TotalRegistros = totalRegistros,
                TotalPaginas = (int)Math.Ceiling(totalRegistros / (double)tamanioPagina)
            });
        }

        // GET: api/logs/auditoria?pagina=1&usuarioId=1&entidad=Producto&accion=Crear&desde=2026-10-01&hasta=2026-10-31
        [HttpGet("auditoria")]
        public async Task<ActionResult<PagedResult<AuditoriaDto>>> GetAuditoria(
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanioPagina = 20,
            [FromQuery] int? usuarioId = null,
            [FromQuery] string? entidad = null,
            [FromQuery] string? accion = null,
            [FromQuery] DateTime? desde = null,
            [FromQuery] DateTime? hasta = null)
        {
            if (pagina < 1) pagina = 1;
            if (tamanioPagina < 1 || tamanioPagina > 100) tamanioPagina = 20;

            var query = _context.Auditorias
                .AsNoTracking()
                .Include(a => a.Usuario)
                .AsQueryable();

            if (usuarioId.HasValue) query = query.Where(a => a.UsuarioId == usuarioId.Value);
            if (!string.IsNullOrWhiteSpace(entidad)) query = query.Where(a => a.Entidad == entidad);
            if (!string.IsNullOrWhiteSpace(accion)) query = query.Where(a => a.Accion == accion);
            if (desde.HasValue) query = query.Where(a => a.Fecha >= desde.Value);
            if (hasta.HasValue) query = query.Where(a => a.Fecha < hasta.Value.Date.AddDays(1));

            query = query.OrderByDescending(a => a.Fecha);

            var totalRegistros = await query.CountAsync();

            var items = await query
                .Skip((pagina - 1) * tamanioPagina)
                .Take(tamanioPagina)
                .Select(a => new AuditoriaDto
                {
                    Id = a.Id,
                    Fecha = a.Fecha,
                    UsuarioId = a.UsuarioId,
                    UsuarioNombre = a.Usuario != null ? a.Usuario.NombreUsuario : null,
                    Accion = a.Accion,
                    Entidad = a.Entidad,
                    EntidadId = a.EntidadId,
                    Detalle = a.Detalle
                })
                .ToListAsync();

            return Ok(new PagedResult<AuditoriaDto>
            {
                Items = items,
                PaginaActual = pagina,
                TamanioPagina = tamanioPagina,
                TotalRegistros = totalRegistros,
                TotalPaginas = (int)Math.Ceiling(totalRegistros / (double)tamanioPagina)
            });
        }
    }
}