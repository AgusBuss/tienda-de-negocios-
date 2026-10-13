using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioAPI.Data;
using InventarioAPI.Models;
using InventarioAPI.DTOs;

namespace InventarioAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = NombresRol.AdminVendedor)]
    public class SalidasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SalidasController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/salidas?pagina=1&tamanioPagina=10
        [HttpGet]
        public async Task<ActionResult<PagedResult<SalidaDto>>> GetSalidas(
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanioPagina = 10)
        {
            if (pagina < 1) pagina = 1;
            if (tamanioPagina < 1 || tamanioPagina > 100) tamanioPagina = 10;

            var query = _context.Salidas
                .AsNoTracking()
                .Include(s => s.Cliente)
                .Include(s => s.Usuario)
                .Include(s => s.Detalles).ThenInclude(d => d.Producto)
                .OrderByDescending(s => s.Fecha);

            var totalRegistros = await query.CountAsync();

            var items = await query
                .Skip((pagina - 1) * tamanioPagina)
                .Take(tamanioPagina)
                .Select(s => new SalidaDto
                {
                    Id = s.Id,
                    Fecha = s.Fecha,
                    ClienteId = s.ClienteId,
                    ClienteNombre = s.Cliente != null ? s.Cliente.Nombre : null,
                    UsuarioId = s.UsuarioId,
                    UsuarioNombre = s.Usuario != null ? s.Usuario.NombreUsuario : null,
                    Total = s.Total,
                    Detalles = s.Detalles.Select(d => new SalidaDetalleDto
                    {
                        ProductoId = d.ProductoId,
                        ProductoNombre = d.Producto != null ? d.Producto.Nombre : null,
                        Cantidad = d.Cantidad,
                        PrecioUnitario = d.PrecioUnitario
                    }).ToList()
                })
                .ToListAsync();

            return Ok(new PagedResult<SalidaDto>
            {
                Items = items,
                PaginaActual = pagina,
                TamanioPagina = tamanioPagina,
                TotalRegistros = totalRegistros,
                TotalPaginas = (int)Math.Ceiling(totalRegistros / (double)tamanioPagina)
            });
        }

        // GET: api/salidas/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SalidaDto>> GetSalida(int id)
        {
            var s = await _context.Salidas
                .Include(x => x.Cliente)
                .Include(x => x.Usuario)
                .Include(x => x.Detalles).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (s == null) return NotFound($"No existe la salida con id {id}.");

            return Ok(new SalidaDto
            {
                Id = s.Id,
                Fecha = s.Fecha,
                ClienteId = s.ClienteId,
                ClienteNombre = s.Cliente?.Nombre,
                UsuarioId = s.UsuarioId,
                UsuarioNombre = s.Usuario?.NombreUsuario,
                Total = s.Total,
                Detalles = s.Detalles.Select(d => new SalidaDetalleDto
                {
                    ProductoId = d.ProductoId,
                    ProductoNombre = d.Producto?.Nombre,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario
                }).ToList()
            });
        }

        // POST: api/salidas
        [HttpPost]
        public async Task<ActionResult<SalidaDto>> CrearSalida(SalidaCreateDto dto)
        {
            if (dto.Detalles == null || dto.Detalles.Count == 0)
                return BadRequest("La salida debe tener al menos un detalle de producto.");

            var clienteExiste = await _context.Clientes.AnyAsync(c => c.Id == dto.ClienteId);
            if (!clienteExiste) return BadRequest("El cliente indicado no existe.");

            var usuarioIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(usuarioIdClaim, out var usuarioId))
                return Unauthorized("No se pudo identificar al usuario del token.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // PASO 1: validar que haya stock suficiente para TODOS los productos
                // antes de descontar nada (si uno falla, no queremos dejar la venta a medias)
                var productosCache = new Dictionary<int, Producto>();

                foreach (var det in dto.Detalles)
                {
                    if (det.Cantidad <= 0)
                        return BadRequest($"La cantidad para el producto {det.ProductoId} debe ser mayor a 0.");

                    var producto = await _context.Productos.FindAsync(det.ProductoId);
                    if (producto == null)
                        return BadRequest($"No existe el producto con id {det.ProductoId}.");

                    if (producto.Stock < det.Cantidad)
                        return BadRequest(
                            $"Stock insuficiente para '{producto.Nombre}'. Stock disponible: {producto.Stock}, solicitado: {det.Cantidad}.");

                    productosCache[det.ProductoId] = producto;
                }

                // PASO 2: si todo tiene stock suficiente, recién ahí armamos la salida y descontamos
                var salida = new Salida
                {
                    Fecha = DateTime.Now,
                    ClienteId = dto.ClienteId,
                    UsuarioId = usuarioId,
                    Total = 0
                };

                decimal total = 0;

                foreach (var det in dto.Detalles)
                {
                    var producto = productosCache[det.ProductoId];

                    salida.Detalles.Add(new SalidaDetalle
                    {
                        ProductoId = det.ProductoId,
                        Cantidad = det.Cantidad,
                        PrecioUnitario = det.PrecioUnitario
                    });

                    // Una salida RESTA stock
                    producto.Stock -= det.Cantidad;

                    total += det.Cantidad * det.PrecioUnitario;
                }

                salida.Total = total;

                _context.Salidas.Add(salida);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await GetSalida(salida.Id);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}