using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioAPI.Data;
using InventarioAPI.DTOs;
using InventarioAPI.Models;
using InventarioAPI.Services;

namespace InventarioAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // cualquier usuario logueado puede comprar
    public class ComprasController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AuditoriaService _auditoria;

        public ComprasController(AppDbContext context, AuditoriaService auditoria)
        {
            _context = context;
            _auditoria = auditoria;
        }

        private bool EsPersonal =>
            User.IsInRole(NombresRol.Admin) || User.IsInRole(NombresRol.Vendedor);

        // POST: api/compras  (checkout del carrito)
        [HttpPost]
        public async Task<ActionResult<CompraResultadoDto>> CrearCompra(CompraCreateDto dto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
                return Unauthorized("No se pudo identificar al usuario del token.");

            if (!Enum.TryParse<MetodoPago>(dto.MetodoPago, true, out var metodo) || !Enum.IsDefined(metodo))
                return BadRequest("Método de pago inválido. Usá Tarjeta, Transferencia o Efectivo.");

            // Si el mismo producto viene en varias líneas, se suman las cantidades
            var pedido = dto.Items
                .GroupBy(i => i.ProductoId)
                .Select(g => new { ProductoId = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
                .ToList();

            if (pedido.Count > 50)
                return BadRequest("El carrito no puede tener más de 50 productos distintos.");

            // PASO 1: validar el carrito y tomar los precios actuales de la base
            var ids = pedido.Select(p => p.ProductoId).ToList();
            var productos = await _context.Productos
                .AsNoTracking()
                .Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            foreach (var item in pedido)
            {
                if (!productos.TryGetValue(item.ProductoId, out var prod) || !prod.Activo)
                    return BadRequest($"El producto con id {item.ProductoId} no está disponible.");

                if (prod.Stock < item.Cantidad)
                    return BadRequest(
                        $"Stock insuficiente para '{prod.Nombre}'. Disponible: {prod.Stock}, solicitado: {item.Cantidad}.");
            }

            // PASO 2: pago simulado (antes de escribir nada en la base)
            string? ultimosDigitos = null;

            if (metodo == MetodoPago.Tarjeta)
            {
                var numero = new string((dto.NumeroTarjeta ?? "").Where(char.IsDigit).ToArray());
                if (numero.Length < 13 || numero.Length > 19 || !LuhnValido(numero))
                    return BadRequest("El número de tarjeta no es válido.");

                var cvv = dto.Cvv ?? "";
                if (cvv.Length < 3 || cvv.Length > 4 || !cvv.All(char.IsDigit))
                    return BadRequest("El código de seguridad no es válido.");

                // Simulación: un CVV 000 representa una tarjeta rechazada por el banco
                if (cvv == "000")
                {
                    await _auditoria.RegistrarAsync("PagoRechazado", "Compra", null,
                        "Pago con tarjeta rechazado (simulado)");
                    return StatusCode(StatusCodes.Status402PaymentRequired,
                        new { error = "El pago fue rechazado por el emisor de la tarjeta." });
                }

                ultimosDigitos = numero[^4..];
            }

            var referencia = $"SIM-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

            // PASO 3: registrar la venta en una transacción
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Ficha de cliente vinculada a la cuenta (se crea en la primera compra)
                var usuario = await _context.Usuarios.FindAsync(usuarioId);
                var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

                if (cliente == null)
                {
                    cliente = new Cliente
                    {
                        Nombre = dto.Nombre,
                        DNI = dto.DNI,
                        Telefono = dto.Telefono,
                        Email = usuario?.Email,
                        Direccion = dto.Direccion,
                        UsuarioId = usuarioId
                    };
                    _context.Clientes.Add(cliente);
                }
                else
                {
                    cliente.Nombre = dto.Nombre;
                    cliente.DNI = dto.DNI;
                    cliente.Telefono = dto.Telefono;
                    cliente.Direccion = dto.Direccion;
                }

                // Descuento atómico: solo descuenta si todavía hay stock suficiente.
                // Evita que dos compras simultáneas vendan la misma unidad dos veces.
                foreach (var item in pedido)
                {
                    var cantidad = item.Cantidad;
                    var filas = await _context.Productos
                        .Where(p => p.Id == item.ProductoId && p.Activo && p.Stock >= cantidad)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - cantidad));

                    if (filas == 0)
                    {
                        // Al salir sin Commit, la transacción se revierte sola
                        return BadRequest(
                            $"El producto '{productos[item.ProductoId].Nombre}' se agotó mientras confirmabas la compra.");
                    }
                }

                var salida = new Salida
                {
                    Fecha = DateTime.Now,
                    Cliente = cliente,
                    UsuarioId = usuarioId,
                    Pago = new Pago
                    {
                        Fecha = DateTime.Now,
                        Metodo = metodo,
                        Referencia = referencia,
                        UltimosDigitos = ultimosDigitos
                    }
                };

                var resultado = new CompraResultadoDto
                {
                    Fecha = salida.Fecha,
                    MetodoPago = metodo.ToString(),
                    Referencia = referencia,
                    UltimosDigitos = ultimosDigitos
                };

                foreach (var item in pedido)
                {
                    var prod = productos[item.ProductoId];

                    salida.Detalles.Add(new SalidaDetalle
                    {
                        ProductoId = prod.Id,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = prod.Precio // el precio lo define el servidor
                    });

                    salida.Movimientos.Add(new MovimientoStock
                    {
                        Fecha = salida.Fecha,
                        ProductoId = prod.Id,
                        Tipo = TipoMovimiento.Salida,
                        Cantidad = -item.Cantidad,
                        UsuarioId = usuarioId,
                        Observacion = "Venta online"
                    });

                    resultado.Items.Add(new CompraItemResultadoDto
                    {
                        ProductoId = prod.Id,
                        ProductoNombre = prod.Nombre,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = prod.Precio,
                        Subtotal = item.Cantidad * prod.Precio
                    });
                }

                resultado.Total = resultado.Items.Sum(i => i.Subtotal);

                _context.Salidas.Add(salida);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                resultado.VentaId = salida.Id;

                await _auditoria.RegistrarAsync("Crear", "Compra", salida.Id,
                    $"Compra online: {resultado.Items.Count} producto(s), total {resultado.Total}, pago {metodo}");

                return Ok(resultado);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // GET: api/compras/mias?pagina=1&tamanioPagina=10
        [HttpGet("mias")]
        public async Task<ActionResult<PagedResult<CompraResultadoDto>>> GetMisCompras(
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanioPagina = 10)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
                return Unauthorized("No se pudo identificar al usuario del token.");

            if (pagina < 1) pagina = 1;
            if (tamanioPagina < 1 || tamanioPagina > 100) tamanioPagina = 10;

            // Compras hechas con la ficha de cliente vinculada a este usuario
            var query = _context.Salidas
                .AsNoTracking()
                .Where(s => s.Cliente!.UsuarioId == usuarioId)
                .OrderByDescending(s => s.Fecha);

            var totalRegistros = await query.CountAsync();

            var salidas = await query
                .Skip((pagina - 1) * tamanioPagina)
                .Take(tamanioPagina)
                .Include(s => s.Pago)
                .Include(s => s.Detalles).ThenInclude(d => d.Producto)
                .ToListAsync();

            return Ok(new PagedResult<CompraResultadoDto>
            {
                Items = salidas.Select(Mapear).ToList(),
                PaginaActual = pagina,
                TamanioPagina = tamanioPagina,
                TotalRegistros = totalRegistros,
                TotalPaginas = (int)Math.Ceiling(totalRegistros / (double)tamanioPagina)
            });
        }

        // GET: api/compras/5  (el dueño de la compra o el personal)
        [HttpGet("{id}")]
        public async Task<ActionResult<CompraResultadoDto>> GetCompra(int id)
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId);

            var salida = await _context.Salidas
                .AsNoTracking()
                .Include(s => s.Cliente)
                .Include(s => s.Pago)
                .Include(s => s.Detalles).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(s => s.Id == id);

            // Si la compra es de otra persona se responde 404 (no se revela que existe)
            if (salida == null || (!EsPersonal && salida.Cliente?.UsuarioId != usuarioId))
                return NotFound($"No existe la compra con id {id}.");

            return Ok(Mapear(salida));
        }

        private static CompraResultadoDto Mapear(Salida s)
        {
            var items = s.Detalles.Select(d => new CompraItemResultadoDto
            {
                ProductoId = d.ProductoId,
                ProductoNombre = d.Producto?.Nombre,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                Subtotal = d.Cantidad * d.PrecioUnitario
            }).ToList();

            return new CompraResultadoDto
            {
                VentaId = s.Id,
                Fecha = s.Fecha,
                Total = items.Sum(i => i.Subtotal),
                MetodoPago = s.Pago?.Metodo.ToString(),
                Referencia = s.Pago?.Referencia,
                UltimosDigitos = s.Pago?.UltimosDigitos,
                Items = items
            };
        }

        // Validación estándar de números de tarjeta (algoritmo de Luhn)
        private static bool LuhnValido(string numero)
        {
            int suma = 0;
            bool duplicar = false;

            for (int i = numero.Length - 1; i >= 0; i--)
            {
                int d = numero[i] - '0';
                if (duplicar)
                {
                    d *= 2;
                    if (d > 9) d -= 9;
                }
                suma += d;
                duplicar = !duplicar;
            }

            return suma % 10 == 0;
        }
    }
}