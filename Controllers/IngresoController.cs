using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioAPI.Data;
using InventarioAPI.Models;
using InventarioAPI.DTOs;
using InventarioAPI.Services;

namespace InventarioAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = NombresRol.AdminVendedor)]
    public class IngresosController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AuditoriaService _auditoria;

        public IngresosController(AppDbContext context, AuditoriaService auditoria)
        {
            _context = context;
            _auditoria = auditoria;
        }

        // GET: api/ingresos?pagina=1&tamanioPagina=10
        [HttpGet]
        public async Task<ActionResult<PagedResult<IngresoDto>>> GetIngresos(
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanioPagina = 10)
        {
            if (pagina < 1) pagina = 1;
            if (tamanioPagina < 1 || tamanioPagina > 100) tamanioPagina = 10;

            var query = _context.Ingresos
                .AsNoTracking()
                .Include(i => i.Proveedor)
                .Include(i => i.Usuario)
                .Include(i => i.Detalles).ThenInclude(d => d.Producto)
                .OrderByDescending(i => i.Fecha);

            var totalRegistros = await query.CountAsync();

            var items = await query
                .Skip((pagina - 1) * tamanioPagina)
                .Take(tamanioPagina)
                .Select(i => new IngresoDto
                {
                    Id = i.Id,
                    Fecha = i.Fecha,
                    ProveedorId = i.ProveedorId,
                    ProveedorNombre = i.Proveedor != null ? i.Proveedor.Nombre : null,
                    UsuarioId = i.UsuarioId,
                    UsuarioNombre = i.Usuario != null ? i.Usuario.NombreUsuario : null,
                    Total = i.Total,
                    Detalles = i.Detalles.Select(d => new IngresoDetalleDto
                    {
                        ProductoId = d.ProductoId,
                        ProductoNombre = d.Producto != null ? d.Producto.Nombre : null,
                        Cantidad = d.Cantidad,
                        PrecioUnitario = d.PrecioUnitario
                    }).ToList()
                })
                .ToListAsync();

            return Ok(new PagedResult<IngresoDto>
            {
                Items = items,
                PaginaActual = pagina,
                TamanioPagina = tamanioPagina,
                TotalRegistros = totalRegistros,
                TotalPaginas = (int)Math.Ceiling(totalRegistros / (double)tamanioPagina)
            });
        }

        // GET: api/ingresos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<IngresoDto>> GetIngreso(int id)
        {
            var i = await _context.Ingresos
                .Include(x => x.Proveedor)
                .Include(x => x.Usuario)
                .Include(x => x.Detalles).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (i == null) return NotFound($"No existe el ingreso con id {id}.");

            return Ok(new IngresoDto
            {
                Id = i.Id,
                Fecha = i.Fecha,
                ProveedorId = i.ProveedorId,
                ProveedorNombre = i.Proveedor?.Nombre,
                UsuarioId = i.UsuarioId,
                UsuarioNombre = i.Usuario?.NombreUsuario,
                Total = i.Total,
                Detalles = i.Detalles.Select(d => new IngresoDetalleDto
                {
                    ProductoId = d.ProductoId,
                    ProductoNombre = d.Producto?.Nombre,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario
                }).ToList()
            });
        }

        // POST: api/ingresos
        [HttpPost]
        public async Task<ActionResult<IngresoDto>> CrearIngreso(IngresoCreateDto dto)
        {
            if (dto.Detalles == null || dto.Detalles.Count == 0)
                return BadRequest("El ingreso debe tener al menos un detalle de producto.");

            var proveedorExiste = await _context.Proveedores.AnyAsync(p => p.Id == dto.ProveedorId);
            if (!proveedorExiste) return BadRequest("El proveedor indicado no existe.");

            // Obtenemos el id del usuario logueado a partir del token JWT
            var usuarioIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(usuarioIdClaim, out var usuarioId))
                return Unauthorized("No se pudo identificar al usuario del token.");

            // Usamos una transacción: si algo falla a mitad de camino, no queda nada a medio guardar
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var ingreso = new Ingreso
                {
                    Fecha = DateTime.Now,
                    ProveedorId = dto.ProveedorId,
                    UsuarioId = usuarioId,
                    Total = 0
                };

                decimal total = 0;

                foreach (var det in dto.Detalles)
                {
                    if (det.Cantidad <= 0)
                        return BadRequest($"La cantidad para el producto {det.ProductoId} debe ser mayor a 0.");

                    var producto = await _context.Productos.FindAsync(det.ProductoId);
                    if (producto == null)
                        return BadRequest($"No existe el producto con id {det.ProductoId}.");

                    ingreso.Detalles.Add(new IngresoDetalle
                    {
                        ProductoId = det.ProductoId,
                        Cantidad = det.Cantidad,
                        PrecioUnitario = det.PrecioUnitario
                    });

                    // Un ingreso SUMA stock
                    producto.Stock += det.Cantidad;

                    total += det.Cantidad * det.PrecioUnitario;
                }

                ingreso.Total = total;

                _context.Ingresos.Add(ingreso);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _auditoria.RegistrarAsync("Crear", "Ingreso", ingreso.Id,
                    $"Entrada de mercadería: {ingreso.Detalles.Count} producto(s), total {ingreso.Total}");

                return await GetIngreso(ingreso.Id);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}