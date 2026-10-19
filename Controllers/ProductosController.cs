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
    public class ProductosController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly AuditoriaService _auditoria;

        public ProductosController(AppDbContext context, IWebHostEnvironment env, AuditoriaService auditoria)
        {
            _context = context;
            _env = env;
            _auditoria = auditoria;
        }

        // Admin y Vendedor pueden ver productos dados de baja
        private bool EsPersonal =>
            User.IsInRole(NombresRol.Admin) || User.IsInRole(NombresRol.Vendedor);

        private int? UsuarioIdActual =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        // GET: api/productos?buscar=coca&soloDisponibles=true&orden=precio_asc&precioMin=5&precioMax=100&categoriaId=2&pagina=1
        // orden: nombre (por defecto), precio_asc, precio_desc, recientes
        [HttpGet]
        public async Task<ActionResult<PagedResult<ProductoDto>>> GetProductos(
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanioPagina = 10,
            [FromQuery] int? categoriaId = null,
            [FromQuery] string? buscar = null,
            [FromQuery] bool soloDisponibles = false,
            [FromQuery] decimal? precioMin = null,
            [FromQuery] decimal? precioMax = null,
            [FromQuery] string? orden = null,
            [FromQuery] bool incluirInactivos = false)
        {
            if (pagina < 1) pagina = 1;
            if (tamanioPagina < 1 || tamanioPagina > 100) tamanioPagina = 10;

            var query = _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Include(p => p.Proveedor)
                .AsQueryable();

            // El público solo ve productos activos; el personal puede pedir también los inactivos
            if (!(incluirInactivos && EsPersonal))
                query = query.Where(p => p.Activo);

            if (categoriaId.HasValue)
                query = query.Where(p => p.CategoriaId == categoriaId.Value);

            // Búsqueda por texto en nombre o descripción
            if (!string.IsNullOrWhiteSpace(buscar))
            {
                var texto = buscar.Trim();
                query = query.Where(p => p.Nombre.Contains(texto)
                    || (p.Descripcion != null && p.Descripcion.Contains(texto)));
            }

            // Disponibilidad: solo productos con stock
            if (soloDisponibles)
                query = query.Where(p => p.Stock > 0);

            if (precioMin.HasValue)
                query = query.Where(p => p.Precio >= precioMin.Value);

            if (precioMax.HasValue)
                query = query.Where(p => p.Precio <= precioMax.Value);

            // Orden
            query = orden?.ToLowerInvariant() switch
            {
                "precio_asc" => query.OrderBy(p => p.Precio).ThenBy(p => p.Nombre),
                "precio_desc" => query.OrderByDescending(p => p.Precio).ThenBy(p => p.Nombre),
                "recientes" => query.OrderByDescending(p => p.Id),
                _ => query.OrderBy(p => p.Nombre)
            };

            var totalRegistros = await query.CountAsync();

            var items = await query
                .Skip((pagina - 1) * tamanioPagina)
                .Take(tamanioPagina)
                .Select(p => new ProductoDto
                {
                    Id = p.Id,
                    Nombre = p.Nombre,
                    Descripcion = p.Descripcion,
                    Precio = p.Precio,
                    Stock = p.Stock,
                    ImagenUrl = p.ImagenUrl,
                    CategoriaId = p.CategoriaId,
                    CategoriaNombre = p.Categoria != null ? p.Categoria.Nombre : null,
                    ProveedorId = p.ProveedorId,
                    ProveedorNombre = p.Proveedor != null ? p.Proveedor.Nombre : null
                })
                .ToListAsync();

            return Ok(new PagedResult<ProductoDto>
            {
                Items = items,
                PaginaActual = pagina,
                TamanioPagina = tamanioPagina,
                TotalRegistros = totalRegistros,
                TotalPaginas = (int)Math.Ceiling(totalRegistros / (double)tamanioPagina)
            });
        }

        // GET: api/productos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductoDto>> GetProducto(int id)
        {
            var p = await _context.Productos
                .Include(x => x.Categoria)
                .Include(x => x.Proveedor)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (p == null || (!p.Activo && !EsPersonal))
                return NotFound($"No existe el producto con id {id}.");

            return Ok(new ProductoDto
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Descripcion = p.Descripcion,
                Precio = p.Precio,
                Stock = p.Stock,
                ImagenUrl = p.ImagenUrl,
                CategoriaId = p.CategoriaId,
                CategoriaNombre = p.Categoria?.Nombre,
                ProveedorId = p.ProveedorId,
                ProveedorNombre = p.Proveedor?.Nombre
            });
        }

        // POST: api/productos
        [HttpPost]
        [Authorize(Roles = NombresRol.AdminVendedor)]
        public async Task<ActionResult<ProductoDto>> CrearProducto(ProductoCreateDto dto)
        {
            if (dto.Precio < 0) return BadRequest("El precio no puede ser negativo.");
            if (dto.Stock < 0) return BadRequest("El stock no puede ser negativo.");

            var categoriaExiste = await _context.Categorias.AnyAsync(c => c.Id == dto.CategoriaId);
            if (!categoriaExiste) return BadRequest("La categoría indicada no existe.");

            // Swagger manda 0 por defecto: lo tratamos como "sin proveedor"
            int? proveedorId = dto.ProveedorId == 0 ? null : dto.ProveedorId;

            if (proveedorId.HasValue)
            {
                var proveedorExiste = await _context.Proveedores.AnyAsync(p => p.Id == proveedorId.Value);
                if (!proveedorExiste) return BadRequest("El proveedor indicado no existe.");
            }

            var producto = new Producto
            {
                Nombre = dto.Nombre,
                Descripcion = dto.Descripcion,
                Precio = dto.Precio,
                Stock = dto.Stock,
                CategoriaId = dto.CategoriaId,
                ProveedorId = proveedorId,
                Activo = true
            };

            // El stock inicial también deja un movimiento registrado
            if (dto.Stock > 0 && UsuarioIdActual.HasValue)
            {
                producto.Movimientos.Add(new MovimientoStock
                {
                    Fecha = DateTime.Now,
                    Tipo = TipoMovimiento.Ajuste,
                    Cantidad = dto.Stock,
                    UsuarioId = UsuarioIdActual.Value,
                    Observacion = "Stock inicial"
                });
            }

            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Crear", "Producto", producto.Id,
                $"Se creó '{producto.Nombre}' (precio {producto.Precio}, stock {producto.Stock})");

            return CreatedAtAction(nameof(GetProducto), new { id = producto.Id }, new ProductoDto
            {
                Id = producto.Id,
                Nombre = producto.Nombre,
                Descripcion = producto.Descripcion,
                Precio = producto.Precio,
                Stock = producto.Stock,
                CategoriaId = producto.CategoriaId,
                ProveedorId = producto.ProveedorId
            });
        }

        // PUT: api/productos/5
        [HttpPut("{id}")]
        [Authorize(Roles = NombresRol.AdminVendedor)]
        public async Task<IActionResult> ActualizarProducto(int id, ProductoCreateDto dto)
        {
            if (dto.Precio < 0) return BadRequest("El precio no puede ser negativo.");
            if (dto.Stock < 0) return BadRequest("El stock no puede ser negativo.");

            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound($"No existe el producto con id {id}.");

            var categoriaExiste = await _context.Categorias.AnyAsync(c => c.Id == dto.CategoriaId);
            if (!categoriaExiste) return BadRequest("La categoría indicada no existe.");

            int? proveedorId = dto.ProveedorId == 0 ? null : dto.ProveedorId;

            if (proveedorId.HasValue)
            {
                var proveedorExiste = await _context.Proveedores.AnyAsync(p => p.Id == proveedorId.Value);
                if (!proveedorExiste) return BadRequest("El proveedor indicado no existe.");
            }

            var detalle = $"'{producto.Nombre}': precio {producto.Precio} -> {dto.Precio}, stock {producto.Stock} -> {dto.Stock}";

            // Si cambia el stock a mano, queda registrado como ajuste
            var diferencia = dto.Stock - producto.Stock;
            if (diferencia != 0 && UsuarioIdActual.HasValue)
            {
                producto.Movimientos.Add(new MovimientoStock
                {
                    Fecha = DateTime.Now,
                    Tipo = TipoMovimiento.Ajuste,
                    Cantidad = diferencia,
                    UsuarioId = UsuarioIdActual.Value,
                    Observacion = "Ajuste manual de stock"
                });
            }

            producto.Nombre = dto.Nombre;
            producto.Descripcion = dto.Descripcion;
            producto.Precio = dto.Precio;
            producto.Stock = dto.Stock;
            producto.CategoriaId = dto.CategoriaId;
            producto.ProveedorId = proveedorId;

            await _context.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Modificar", "Producto", id, detalle);

            return NoContent();
        }

        // DELETE: api/productos/5
        // Sin movimientos: se borra. Con movimientos: baja lógica (queda inactivo, se conserva el historial).
        [HttpDelete("{id}")]
        [Authorize(Roles = NombresRol.AdminVendedor)]
        public async Task<IActionResult> EliminarProducto(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound($"No existe el producto con id {id}.");

            var tieneHistorial = await _context.IngresoDetalles.AnyAsync(d => d.ProductoId == id)
                || await _context.SalidaDetalles.AnyAsync(d => d.ProductoId == id)
                || await _context.MovimientosStock.AnyAsync(m => m.ProductoId == id);

            if (tieneHistorial)
            {
                if (!producto.Activo)
                    return BadRequest("El producto ya está dado de baja.");

                producto.Activo = false;
                await _context.SaveChangesAsync();

                await _auditoria.RegistrarAsync("Baja", "Producto", id,
                    $"Baja lógica de '{producto.Nombre}' (tiene historial de movimientos)");

                return Ok(new { mensaje = "El producto tiene historial, por lo que se dio de baja en lugar de borrarse." });
            }

            var nombre = producto.Nombre;

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Eliminar", "Producto", id, $"Se eliminó '{nombre}'");

            return NoContent();
        }

        // POST: api/productos/5/imagen
        [HttpPost("{id}/imagen")]
        [Authorize(Roles = NombresRol.AdminVendedor)]
        public async Task<IActionResult> SubirImagen(int id, IFormFile archivo)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound($"No existe el producto con id {id}.");

            if (archivo == null || archivo.Length == 0)
                return BadRequest("No se envió ningún archivo.");

            var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();

            if (!extensionesPermitidas.Contains(extension))
                return BadRequest("Formato de imagen no permitido. Usá jpg, jpeg, png o webp.");

            const long tamanioMaximo = 5 * 1024 * 1024; // 5 MB
            if (archivo.Length > tamanioMaximo)
                return BadRequest("La imagen no puede superar los 5 MB.");

            var carpetaUploads = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
            if (!Directory.Exists(carpetaUploads))
                Directory.CreateDirectory(carpetaUploads);

            // Si el producto ya tenía una imagen previa, la borramos para no acumular archivos huérfanos
            if (!string.IsNullOrEmpty(producto.ImagenUrl))
            {
                var rutaAnterior = Path.Combine(_env.WebRootPath ?? "wwwroot", producto.ImagenUrl.TrimStart('/'));
                if (System.IO.File.Exists(rutaAnterior))
                    System.IO.File.Delete(rutaAnterior);
            }

            var nombreArchivo = $"producto_{id}_{Guid.NewGuid()}{extension}";
            var rutaCompleta = Path.Combine(carpetaUploads, nombreArchivo);

            using (var stream = new FileStream(rutaCompleta, FileMode.Create))
            {
                await archivo.CopyToAsync(stream);
            }

            producto.ImagenUrl = $"/uploads/{nombreArchivo}";
            await _context.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Modificar", "Producto", id,
                $"Se actualizó la imagen de '{producto.Nombre}'");

            return Ok(new { imagenUrl = producto.ImagenUrl });
        }
    }
}