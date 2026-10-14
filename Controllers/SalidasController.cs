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
    public class SalidasController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AuditoriaService _auditoria;

        public SalidasController(AppDbContext context, AuditoriaService auditoria)
        {
            _context = context;
            _auditoria = auditoria;
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
                    // El total se calcula, no se guarda (3FN)
                    Total = s.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario),
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
                Total = s.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario),
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

            if (dto.Detalles.Any(d => d.Cantidad <= 0))
                return BadRequest("Todas las cantidades deben ser mayores a 0.");

            // Si el mismo producto viene en varias líneas, se suman las cantidades
            var pedido = dto.Detalles
                .GroupBy(d => d.ProductoId)
                .Select(g => new { ProductoId = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
                .ToList();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // PASO 1: validar existencia, estado activo y stock de TODOS los productos
                // antes de descontar nada (si uno falla, no queremos dejar la venta a medias)
                var productos = new Dictionary<int, Producto>();

                foreach (var item in pedido)
                {
                    var producto = await _context.Productos.FindAsync(item.ProductoId);
                    if (producto == null)
                        return BadRequest($"No existe el producto con id {item.ProductoId}.");

                    if (!producto.Activo)
                        return BadRequest($"El producto '{producto.Nombre}' no está disponible para la venta.");

                    if (producto.Stock < item.Cantidad)
                        return BadRequest(
                            $"Stock insuficiente para '{producto.Nombre}'. Stock disponible: {producto.Stock}, solicitado: {item.Cantidad}.");

                    productos[item.ProductoId] = producto;
                }

                // PASO 2: armar la salida con el precio ACTUAL del producto y descontar el stock
                var salida = new Salida
                {
                    Fecha = DateTime.Now,
                    ClienteId = dto.ClienteId,
                    UsuarioId = usuarioId
                };

                decimal total = 0;

                foreach (var item in pedido)
                {
                    var producto = productos[item.ProductoId];

                    salida.Detalles.Add(new SalidaDetalle
                    {
                        ProductoId = item.ProductoId,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = producto.Precio // el precio lo define el servidor
                    });

                    // Una salida RESTA stock y deja el movimiento registrado
                    producto.Stock -= item.Cantidad;

                    salida.Movimientos.Add(new MovimientoStock
                    {
                        Fecha = salida.Fecha,
                        ProductoId = item.ProductoId,
                        Tipo = TipoMovimiento.Salida,
                        Cantidad = -item.Cantidad,
                        UsuarioId = usuarioId,
                        Observacion = "Venta"
                    });

                    total += item.Cantidad * producto.Precio;
                }

                _context.Salidas.Add(salida);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _auditoria.RegistrarAsync("Crear", "Salida", salida.Id,
                    $"Venta: {salida.Detalles.Count} producto(s), total {total}");

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