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
    [Authorize(Roles = NombresRol.Admin)]
    public class UsuariosController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AuditoriaService _auditoria;

        public UsuariosController(AppDbContext context, AuditoriaService auditoria)
        {
            _context = context;
            _auditoria = auditoria;
        }

        // GET: api/usuarios
        [HttpGet]
        public async Task<ActionResult<List<UsuarioListadoDto>>> GetUsuarios()
        {
            var usuarios = await _context.Usuarios
                .AsNoTracking()
                .OrderBy(u => u.NombreUsuario)
                .Select(u => new UsuarioListadoDto
                {
                    Id = u.Id,
                    NombreUsuario = u.NombreUsuario,
                    Email = u.Email,
                    Rol = u.Rol!.Nombre
                })
                .ToListAsync();

            return Ok(usuarios);
        }

        // POST: api/usuarios  (el Admin crea un usuario con el rol que quiera)
        [HttpPost]
        public async Task<ActionResult<UsuarioListadoDto>> CrearUsuario(UsuarioAdminCreateDto dto)
        {
            var rol = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == dto.Rol);
            if (rol == null) return BadRequest($"El rol '{dto.Rol}' no existe.");

            if (await _context.Usuarios.AnyAsync(u => u.NombreUsuario == dto.NombreUsuario))
                return BadRequest("Ya existe un usuario con ese nombre.");

            var usuario = new Usuario
            {
                NombreUsuario = dto.NombreUsuario,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Email = dto.Email,
                RolId = rol.Id
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Crear", "Usuario", usuario.Id,
                $"Se creó el usuario '{usuario.NombreUsuario}' con rol {rol.Nombre}");

            return Ok(new UsuarioListadoDto
            {
                Id = usuario.Id,
                NombreUsuario = usuario.NombreUsuario,
                Email = usuario.Email,
                Rol = rol.Nombre
            });
        }

        // PUT: api/usuarios/5/rol
        [HttpPut("{id}/rol")]
        public async Task<IActionResult> CambiarRol(int id, CambiarRolDto dto)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (usuario == null) return NotFound($"No existe el usuario con id {id}.");

            var rol = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == dto.Rol);
            if (rol == null) return BadRequest($"El rol '{dto.Rol}' no existe.");

            // Evita que el admin se quite a sí mismo el rol y deje el sistema sin administradores
            var miId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (miId == id.ToString() && rol.Nombre != NombresRol.Admin)
                return BadRequest("No podés quitarte a vos mismo el rol de Admin.");

            var rolAnterior = usuario.Rol?.Nombre;

            usuario.RolId = rol.Id;
            await _context.SaveChangesAsync();

            await _auditoria.RegistrarAsync("Modificar", "Usuario", id,
                $"Cambio de rol de '{usuario.NombreUsuario}': {rolAnterior} -> {rol.Nombre}");

            return NoContent();
        }
    }
}