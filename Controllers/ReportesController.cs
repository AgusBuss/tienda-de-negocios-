using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioAPI.Data;
using InventarioAPI.Models;
using InventarioAPI.Services;

namespace InventarioAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = NombresRol.AdminVendedor)]
    public class ReportesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ReporteService _reportes;
        private readonly EmailService _email;
        private readonly AuditoriaService _auditoria;

        public ReportesController(
            AppDbContext context,
            ReporteService reportes,
            EmailService email,
            AuditoriaService auditoria)
        {
            _context = context;
            _reportes = reportes;
            _email = email;
            _auditoria = auditoria;
        }

        // GET: api/reportes/stock
        [HttpGet("stock")]
        public async Task<IActionResult> DescargarStock()
        {
            var pdf = await _reportes.StockAsync();
            await _auditoria.RegistrarAsync("Descargar", "Reporte", null, "Reporte de stock");
            return File(pdf, "application/pdf", NombreArchivo("stock", null, null));
        }

        // GET: api/reportes/entradas?desde=2026-10-01&hasta=2026-10-31
        [HttpGet("entradas")]
        public async Task<IActionResult> DescargarEntradas([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var (d, h, error) = ResolverRango(desde, hasta);
            if (error != null) return BadRequest(error);

            var pdf = await _reportes.EntradasAsync(d, h);
            await _auditoria.RegistrarAsync("Descargar", "Reporte", null,
                $"Reporte de entradas {d:dd/MM/yyyy} al {h:dd/MM/yyyy}");
            return File(pdf, "application/pdf", NombreArchivo("entradas", d, h));
        }

        // GET: api/reportes/salidas?desde=2026-10-01&hasta=2026-10-31
        [HttpGet("salidas")]
        public async Task<IActionResult> DescargarSalidas([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var (d, h, error) = ResolverRango(desde, hasta);
            if (error != null) return BadRequest(error);

            var pdf = await _reportes.SalidasAsync(d, h);
            await _auditoria.RegistrarAsync("Descargar", "Reporte", null,
                $"Reporte de salidas {d:dd/MM/yyyy} al {h:dd/MM/yyyy}");
            return File(pdf, "application/pdf", NombreArchivo("salidas", d, h));
        }

        // POST: api/reportes/enviar
        [HttpPost("enviar")]
        public async Task<IActionResult> Enviar(EnviarReporteDto dto)
        {
            var tipo = dto.Tipo.Trim().ToLowerInvariant();
            if (tipo != "stock" && tipo != "entradas" && tipo != "salidas")
                return BadRequest("Tipo de reporte inválido. Usá stock, entradas o salidas.");

            // Destinatario: el indicado, o el email del usuario logueado
            var destinatario = dto.Destinatario?.Trim();
            if (string.IsNullOrEmpty(destinatario))
            {
                int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId);
                destinatario = await _context.Usuarios
                    .Where(u => u.Id == usuarioId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrWhiteSpace(destinatario))
                    return BadRequest("Indicá un destinatario: tu usuario no tiene email cargado.");
            }

            byte[] pdf;
            string nombre;
            string descripcion;
            DateTime d = default, h = default;

            if (tipo == "stock")
            {
                pdf = await _reportes.StockAsync();
                nombre = NombreArchivo("stock", null, null);
                descripcion = "Reporte de stock actual";
            }
            else
            {
                var (desde, hasta, error) = ResolverRango(dto.Desde, dto.Hasta);
                if (error != null) return BadRequest(error);
                d = desde;
                h = hasta;

                pdf = tipo == "entradas"
                    ? await _reportes.EntradasAsync(d, h)
                    : await _reportes.SalidasAsync(d, h);
                nombre = NombreArchivo(tipo, d, h);
                descripcion = $"Reporte de {tipo} del {d:dd/MM/yyyy} al {h:dd/MM/yyyy}";
            }

            try
            {
                await _email.EnviarConAdjuntoAsync(
                    destinatario,
                    $"{descripcion} - Tienda de Negocios",
                    $"Hola,\n\nTe enviamos adjunto el siguiente documento: {descripcion}.\n\nGenerado el {DateTime.Now:dd/MM/yyyy HH:mm}.",
                    pdf,
                    nombre);
            }
            catch (EmailException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
            }

            await _auditoria.RegistrarAsync("Enviar", "Reporte", null, $"{descripcion} enviado a {destinatario}");

            return Ok(new { mensaje = $"El reporte se envió a {destinatario}." });
        }

        // Si no se indican fechas: últimos 30 días. Máximo un año de rango.
        private static (DateTime desde, DateTime hasta, string? error) ResolverRango(DateTime? desde, DateTime? hasta)
        {
            var h = (hasta ?? DateTime.Today).Date;
            var d = (desde ?? h.AddDays(-30)).Date;

            if (d > h) return (d, h, "La fecha 'desde' no puede ser posterior a 'hasta'.");
            if ((h - d).TotalDays > 366) return (d, h, "El rango máximo permitido es de un año.");

            return (d, h, null);
        }

        private static string NombreArchivo(string tipo, DateTime? desde, DateTime? hasta) =>
            desde.HasValue && hasta.HasValue
                ? $"reporte-{tipo}-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf"
                : $"reporte-{tipo}-{DateTime.Now:yyyyMMdd}.pdf";
    }

    public class EnviarReporteDto
    {
        // "stock", "entradas" o "salidas"
        [Required]
        public string Tipo { get; set; } = string.Empty;

        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }

        // Opcional: si falta, se usa el email del usuario logueado
        [MaxLength(150)]
        public string? Destinatario { get; set; }
    }
}