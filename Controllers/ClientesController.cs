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
    public class ClientesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClientesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<ClienteDto>>> GetClientes(
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanioPagina = 10)
        {
            if (pagina < 1) pagina = 1;
            if (tamanioPagina < 1 || tamanioPagina > 100) tamanioPagina = 10;

            var query = _context.Clientes.AsNoTracking().OrderBy(c => c.Nombre);
            var totalRegistros = await query.CountAsync();

            var items = await query
                .Skip((pagina - 1) * tamanioPagina)
                .Take(tamanioPagina)
                .Select(c => new ClienteDto
                {
                    Id = c.Id,
                    Nombre = c.Nombre,
                    DNI = c.DNI,
                    Telefono = c.Telefono,
                    Email = c.Email,
                    Direccion = c.Direccion
                })
                .ToListAsync();

            return Ok(new PagedResult<ClienteDto>
            {
                Items = items,
                PaginaActual = pagina,
                TamanioPagina = tamanioPagina,
                TotalRegistros = totalRegistros,
                TotalPaginas = (int)Math.Ceiling(totalRegistros / (double)tamanioPagina)
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ClienteDto>> GetCliente(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return NotFound($"No existe el cliente con id {id}.");

            return Ok(new ClienteDto
            {
                Id = cliente.Id,
                Nombre = cliente.Nombre,
                DNI = cliente.DNI,
                Telefono = cliente.Telefono,
                Email = cliente.Email,
                Direccion = cliente.Direccion
            });
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ClienteDto>> CrearCliente(ClienteCreateDto dto)
        {
            var cliente = new Cliente
            {
                Nombre = dto.Nombre,
                DNI = dto.DNI,
                Telefono = dto.Telefono,
                Email = dto.Email,
                Direccion = dto.Direccion
            };

            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var resultado = new ClienteDto
            {
                Id = cliente.Id,
                Nombre = cliente.Nombre,
                DNI = cliente.DNI,
                Telefono = cliente.Telefono,
                Email = cliente.Email,
                Direccion = cliente.Direccion
            };

            return CreatedAtAction(nameof(GetCliente), new { id = cliente.Id }, resultado);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> ActualizarCliente(int id, ClienteCreateDto dto)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return NotFound($"No existe el cliente con id {id}.");

            cliente.Nombre = dto.Nombre;
            cliente.DNI = dto.DNI;
            cliente.Telefono = dto.Telefono;
            cliente.Email = dto.Email;
            cliente.Direccion = dto.Direccion;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> EliminarCliente(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return NotFound($"No existe el cliente con id {id}.");

            var tieneSalidas = await _context.Salidas.AnyAsync(s => s.ClienteId == id);
            if (tieneSalidas)
                return BadRequest("No se puede eliminar el cliente porque tiene ventas asociadas.");

            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}