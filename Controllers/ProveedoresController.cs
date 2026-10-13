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
    public class ProveedoresController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProveedoresController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<ProveedorDto>>> GetProveedores(
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanioPagina = 10)
        {
            if (pagina < 1) pagina = 1;
            if (tamanioPagina < 1 || tamanioPagina > 100) tamanioPagina = 10;

            var query = _context.Proveedores.AsNoTracking().OrderBy(p => p.Nombre);
            var totalRegistros = await query.CountAsync();

            var items = await query
                .Skip((pagina - 1) * tamanioPagina)
                .Take(tamanioPagina)
                .Select(p => new ProveedorDto
                {
                    Id = p.Id,
                    Nombre = p.Nombre,
                    CUIT = p.CUIT,
                    Telefono = p.Telefono,
                    Email = p.Email,
                    Direccion = p.Direccion
                })
                .ToListAsync();

            return Ok(new PagedResult<ProveedorDto>
            {
                Items = items,
                PaginaActual = pagina,
                TamanioPagina = tamanioPagina,
                TotalRegistros = totalRegistros,
                TotalPaginas = (int)Math.Ceiling(totalRegistros / (double)tamanioPagina)
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProveedorDto>> GetProveedor(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null) return NotFound($"No existe el proveedor con id {id}.");

            return Ok(new ProveedorDto
            {
                Id = proveedor.Id,
                Nombre = proveedor.Nombre,
                CUIT = proveedor.CUIT,
                Telefono = proveedor.Telefono,
                Email = proveedor.Email,
                Direccion = proveedor.Direccion
            });
        }

        [HttpPost]
        public async Task<ActionResult<ProveedorDto>> CrearProveedor(ProveedorCreateDto dto)
        {
            var proveedor = new Proveedor
            {
                Nombre = dto.Nombre,
                CUIT = dto.CUIT,
                Telefono = dto.Telefono,
                Email = dto.Email,
                Direccion = dto.Direccion
            };

            _context.Proveedores.Add(proveedor);
            await _context.SaveChangesAsync();

            var resultado = new ProveedorDto
            {
                Id = proveedor.Id,
                Nombre = proveedor.Nombre,
                CUIT = proveedor.CUIT,
                Telefono = proveedor.Telefono,
                Email = proveedor.Email,
                Direccion = proveedor.Direccion
            };

            return CreatedAtAction(nameof(GetProveedor), new { id = proveedor.Id }, resultado);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarProveedor(int id, ProveedorCreateDto dto)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null) return NotFound($"No existe el proveedor con id {id}.");

            proveedor.Nombre = dto.Nombre;
            proveedor.CUIT = dto.CUIT;
            proveedor.Telefono = dto.Telefono;
            proveedor.Email = dto.Email;
            proveedor.Direccion = dto.Direccion;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarProveedor(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null) return NotFound($"No existe el proveedor con id {id}.");

            var tieneProductos = await _context.Productos.AnyAsync(p => p.ProveedorId == id);
            var tieneIngresos = await _context.Ingresos.AnyAsync(i => i.ProveedorId == id);
            if (tieneProductos || tieneIngresos)
                return BadRequest("No se puede eliminar el proveedor porque tiene productos o ingresos asociados.");

            _context.Proveedores.Remove(proveedor);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}