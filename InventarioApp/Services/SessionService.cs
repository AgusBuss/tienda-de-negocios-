using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace InventarioApp.Services;

/// <summary>
/// Guarda el token JWT de forma segura (SecureStorage) y expone
/// el usuario y su rol para mostrar u ocultar opciones en la app.
/// </summary>
public class SessionService
{
    private const string TokenKey = "jwt_token";

    public string? Token { get; private set; }
    public string? Usuario { get; private set; }
    public string? Rol { get; private set; }

    public bool EstaLogueado => !string.IsNullOrEmpty(Token);
    public bool EsAdmin => Rol == "Admin";
    public bool EsPersonal => Rol == "Admin" || Rol == "Vendedor";

    /// <summary>Se dispara cuando cambia la sesión (login/logout) para refrescar la UI.</summary>
    public event Action? SesionCambiada;

    /// <summary>Se llama una vez al arrancar la app para recuperar la sesión guardada.</summary>
    public async Task InicializarAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync(TokenKey);
            if (!string.IsNullOrEmpty(token))
                AplicarToken(token);
        }
        catch
        {
            // SecureStorage puede fallar en algunos dispositivos; se ignora y se pide login.
            SecureStorage.Default.Remove(TokenKey);
        }
    }

    public async Task GuardarSesionAsync(string token)
    {
        AplicarToken(token);
        try { await SecureStorage.Default.SetAsync(TokenKey, token); }
        catch { /* si no se puede persistir, la sesión queda solo en memoria */ }
        SesionCambiada?.Invoke();
    }

    public void CerrarSesion()
    {
        Token = null;
        Usuario = null;
        Rol = null;
        SecureStorage.Default.Remove(TokenKey);
        SesionCambiada?.Invoke();
    }

    private void AplicarToken(string token)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Si el token ya venció, se descarta.
        if (jwt.ValidTo < DateTime.UtcNow)
        {
            CerrarSesionSilenciosa();
            return;
        }

        Token = token;
        Usuario = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == "unique_name")?.Value;
        Rol = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role")?.Value;
    }

    private void CerrarSesionSilenciosa()
    {
        Token = null;
        Usuario = null;
        Rol = null;
        SecureStorage.Default.Remove(TokenKey);
    }
}