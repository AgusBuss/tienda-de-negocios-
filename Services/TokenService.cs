using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using InventarioAPI.Models;

namespace InventarioAPI.Services
{
    public class TokenService
    {
        private readonly IConfiguration _config;

        public TokenService(IConfiguration config)
        {
            _config = config;
        }

        public (string token, DateTime expira) GenerarToken(Usuario usuario)
        {
            var claves = _config["Jwt:Key"] ?? throw new InvalidOperationException("Falta Jwt:Key en la configuración.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(claves));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.NombreUsuario),
                new Claim(ClaimTypes.Role, usuario.Rol!.Nombre)
            };

            var expira = DateTime.UtcNow.AddHours(8);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expira,
                signingCredentials: creds
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expira);
        }
    }
}