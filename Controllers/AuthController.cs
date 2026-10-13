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
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly TokenService _tokenService;

        public AuthController(AppDbContext context, TokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        // POST: api/auth/registro
        // El registro público SIEMPRE crea un usuario con rol Cliente.
        // Los roles Admin y Vendedor los asigna un Admin desde api/usuarios.
        [HttpPost("registro")]
        public async Task<ActionResult<AuthResponseDto>> Registro(RegistroDto dto)
        {
            var existe = await _context.Usuarios.AnyAsync(u => u.NombreUsuario == dto.NombreUsuario);
            if (existe)
                return BadRequest("Ya existe un usuario con ese nombre.");

            var rolCliente = await _context.Roles.FirstAsync(r => r.Nombre == NombresRol.Cliente);

            var usuario = new Usuario
            {
                NombreUsuario = dto.NombreUsuario,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Email = dto.Email,
                RolId = rolCliente.Id,
                Rol = rolCliente
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            var (token, expira) = _tokenService.GenerarToken(usuario);

            return Ok(new AuthResponseDto
            {
                Id = usuario.Id,
                NombreUsuario = usuario.NombreUsuario,
                Rol = rolCliente.Nombre,
                Token = token,
                Expira = expira
            });
        }

        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.NombreUsuario == dto.NombreUsuario);

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
                return Unauthorized("Usuario o contraseña incorrectos.");

            var (token, expira) = _tokenService.GenerarToken(usuario);

            return Ok(new AuthResponseDto
            {
                Id = usuario.Id,
                NombreUsuario = usuario.NombreUsuario,
                Rol = usuario.Rol!.Nombre,
                Token = token,
                Expira = expira
            });
        }
    }
}