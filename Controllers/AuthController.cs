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
        [HttpPost("registro")]
        public async Task<ActionResult<AuthResponseDto>> Registro(RegistroDto dto)
        {
            var existe = await _context.Usuarios.AnyAsync(u => u.NombreUsuario == dto.NombreUsuario);
            if (existe)
                return BadRequest("Ya existe un usuario con ese nombre.");

            var usuario = new Usuario
            {
                NombreUsuario = dto.NombreUsuario,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Email = dto.Email,
                Rol = dto.Rol
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            var (token, expira) = _tokenService.GenerarToken(usuario);

            return Ok(new AuthResponseDto
            {
                Id = usuario.Id,
                NombreUsuario = usuario.NombreUsuario,
                Rol = usuario.Rol,
                Token = token,
                Expira = expira
            });
        }

        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
        {
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.NombreUsuario == dto.NombreUsuario);

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
                return Unauthorized("Usuario o contraseña incorrectos.");

            var (token, expira) = _tokenService.GenerarToken(usuario);

            return Ok(new AuthResponseDto
            {
                Id = usuario.Id,
                NombreUsuario = usuario.NombreUsuario,
                Rol = usuario.Rol,
                Token = token,
                Expira = expira
            });
        }
    }
}