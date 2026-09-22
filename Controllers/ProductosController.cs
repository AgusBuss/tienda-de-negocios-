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
    public class ProductosController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProductosController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: api/productos?pagina=1&tamanioPagina=10&categoriaId=2
        [HttpGet]
        public async Task<ActionResult<PagedResult<ProductoDto>>> GetProductos(
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanioPagina = 10,
            [FromQuery] int? categoriaId = null)
        {
            if (pagina < 1) pagina = 1;
            if (tamanioPagina < 1 || tamanioPagina > 100) tamanioPagina = 10;

            var query = _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Include(p => p.Proveedor)
                .AsQueryable();

            if (categoriaId.HasValue)
                query = query.Where(p => p.CategoriaId == categoriaId.Value);

            query = query.OrderBy(p => p.Nombre);

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

            if (p == null) return NotFound($"No existe el producto con id {id}.");

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
        [Authorize]
        public async Task<ActionResult<ProductoDto>> CrearProducto(ProductoCreateDto dto)
        {
            var categoriaExiste = await _context.Categorias.AnyAsync(c => c.Id == dto.CategoriaId);
            if (!categoriaExiste) return BadRequest("La categoría indicada no existe.");

            if (dto.ProveedorId.HasValue)
            {
                var proveedorExiste = await _context.Proveedores.AnyAsync(p => p.Id == dto.ProveedorId.Value);
                if (!proveedorExiste) return BadRequest("El proveedor indicado no existe.");
            }

            var producto = new Producto
            {
                Nombre = dto.Nombre,
                Descripcion = dto.Descripcion,
                Precio = dto.Precio,
                Stock = dto.Stock,
                CategoriaId = dto.CategoriaId,
                ProveedorId = dto.ProveedorId
            };

            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();

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
        [Authorize]
        public async Task<IActionResult> ActualizarProducto(int id, ProductoCreateDto dto)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound($"No existe el producto con id {id}.");

            var categoriaExiste = await _context.Categorias.AnyAsync(c => c.Id == dto.CategoriaId);
            if (!categoriaExiste) return BadRequest("La categoría indicada no existe.");

            producto.Nombre = dto.Nombre;
            producto.Descripcion = dto.Descripcion;
            producto.Precio = dto.Precio;
            producto.Stock = dto.Stock;
            producto.CategoriaId = dto.CategoriaId;
            producto.ProveedorId = dto.ProveedorId;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/productos/5
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> EliminarProducto(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound($"No existe el producto con id {id}.");

            var tieneMovimientos = await _context.IngresoDetalles.AnyAsync(d => d.ProductoId == id)
                || await _context.SalidaDetalles.AnyAsync(d => d.ProductoId == id);

            if (tieneMovimientos)
                return BadRequest("No se puede eliminar el producto porque tiene movimientos de stock asociados.");

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/productos/5/imagen
        [HttpPost("{id}/imagen")]
        [Authorize]
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

            return Ok(new { imagenUrl = producto.ImagenUrl });
        }
    }
}