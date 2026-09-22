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
    public class CategoriasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoriasController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/categorias?pagina=1&tamanioPagina=10
        [HttpGet]
        public async Task<ActionResult<PagedResult<CategoriaDto>>> GetCategorias(
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanioPagina = 10)
        {
            if (pagina < 1) pagina = 1;
            if (tamanioPagina < 1 || tamanioPagina > 100) tamanioPagina = 10;

            var query = _context.Categorias.AsNoTracking().OrderBy(c => c.Nombre);

            var totalRegistros = await query.CountAsync();

            var items = await query
                .Skip((pagina - 1) * tamanioPagina)
                .Take(tamanioPagina)
                .Select(c => new CategoriaDto
                {
                    Id = c.Id,
                    Nombre = c.Nombre,
                    Descripcion = c.Descripcion
                })
                .ToListAsync();

            return Ok(new PagedResult<CategoriaDto>
            {
                Items = items,
                PaginaActual = pagina,
                TamanioPagina = tamanioPagina,
                TotalRegistros = totalRegistros,
                TotalPaginas = (int)Math.Ceiling(totalRegistros / (double)tamanioPagina)
            });
        }

        // GET: api/categorias/5
        [HttpGet("{id}")]
        public async Task<ActionResult<CategoriaDto>> GetCategoria(int id)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria == null) return NotFound($"No existe la categoría con id {id}.");

            return Ok(new CategoriaDto
            {
                Id = categoria.Id,
                Nombre = categoria.Nombre,
                Descripcion = categoria.Descripcion
            });
        }

        // POST: api/categorias
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<CategoriaDto>> CrearCategoria(CategoriaCreateDto dto)
        {
            var categoria = new Categoria
            {
                Nombre = dto.Nombre,
                Descripcion = dto.Descripcion
            };

            _context.Categorias.Add(categoria);
            await _context.SaveChangesAsync();

            var resultado = new CategoriaDto
            {
                Id = categoria.Id,
                Nombre = categoria.Nombre,
                Descripcion = categoria.Descripcion
            };

            return CreatedAtAction(nameof(GetCategoria), new { id = categoria.Id }, resultado);
        }

        // PUT: api/categorias/5
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> ActualizarCategoria(int id, CategoriaCreateDto dto)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria == null) return NotFound($"No existe la categoría con id {id}.");

            categoria.Nombre = dto.Nombre;
            categoria.Descripcion = dto.Descripcion;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/categorias/5
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> EliminarCategoria(int id)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria == null) return NotFound($"No existe la categoría con id {id}.");

            var tieneProductos = await _context.Productos.AnyAsync(p => p.CategoriaId == id);
            if (tieneProductos)
                return BadRequest("No se puede eliminar la categoría porque tiene productos asociados.");

            _context.Categorias.Remove(categoria);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}