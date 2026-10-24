using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InventarioApp.Models;

namespace InventarioApp.Services;

/// <summary>Gestión de usuarios y roles (solo Admin). Usa el mismo ApiResult que ApiClient.</summary>
public class UsuariosClient
{
    private const string Ruta = "api/Usuarios";

    private readonly HttpClient _http;
    private readonly SessionService _sesion;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public UsuariosClient(HttpClient http, SessionService sesion)
    {
        _http = http;
        _sesion = sesion;
        _http.BaseAddress ??= new Uri(ApiConfig.BaseUrl.TrimEnd('/') + "/");
    }

    /// <summary>Lista de usuarios; acepta arreglo simple o paginado con "items".</summary>
    public async Task<ApiResult<List<UsuarioListadoDto>>> GetUsuariosAsync()
    {
        var r = await EnviarAsync(HttpMethod.Get, Ruta, null);
        if (!r.Ok)
            return new ApiResult<List<UsuarioListadoDto>> { Ok = false, Error = r.Error, StatusCode = r.StatusCode };

        try
        {
            using var doc = JsonDocument.Parse(r.Data ?? "[]");
            var el = doc.RootElement;
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("items", out var items))
                el = items;
            var lista = el.Deserialize<List<UsuarioListadoDto>>(JsonOpts) ?? new();
            return new ApiResult<List<UsuarioListadoDto>> { Ok = true, Data = lista, StatusCode = r.StatusCode };
        }
        catch (Exception)
        {
            return new ApiResult<List<UsuarioListadoDto>> { Ok = false, Error = "No se pudo leer la lista de usuarios.", StatusCode = r.StatusCode };
        }
    }

    public Task<ApiResult<string>> CrearUsuarioAsync(UsuarioAdminCreateDto dto) =>
        EnviarAsync(HttpMethod.Post, Ruta, dto);

    public Task<ApiResult<string>> CambiarRolAsync(int id, string rol) =>
        EnviarAsync(HttpMethod.Put, $"{Ruta}/{id}/rol", new CambiarRolDto { Rol = rol });

    // ---------- Núcleo ----------

    private async Task<ApiResult<string>> EnviarAsync(HttpMethod metodo, string ruta, object? cuerpo)
    {
        try
        {
            using var req = new HttpRequestMessage(metodo, ruta);

            if (_sesion.EstaLogueado)
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sesion.Token);

            if (cuerpo is not null)
                req.Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req);
            var texto = await resp.Content.ReadAsStringAsync();
            var codigo = (int)resp.StatusCode;

            if (resp.IsSuccessStatusCode)
                return new ApiResult<string> { Ok = true, Data = texto, StatusCode = codigo };

            if (resp.StatusCode == HttpStatusCode.Unauthorized && _sesion.EstaLogueado)
                _sesion.CerrarSesion();

            return new ApiResult<string> { Ok = false, StatusCode = codigo, Error = ExtraerError(texto, resp.StatusCode) };
        }
        catch (TaskCanceledException)
        {
            return new ApiResult<string> { Ok = false, Error = "El servidor tardó demasiado en responder." };
        }
        catch (HttpRequestException)
        {
            return new ApiResult<string> { Ok = false, Error = "No se pudo conectar con el servidor." };
        }
    }

    private static string ExtraerError(string cuerpo, HttpStatusCode estado)
    {
        if (!string.IsNullOrWhiteSpace(cuerpo))
        {
            try
            {
                using var doc = JsonDocument.Parse(cuerpo);
                var raiz = doc.RootElement;

                if (raiz.ValueKind == JsonValueKind.String)
                    return raiz.GetString() ?? "";

                if (raiz.ValueKind == JsonValueKind.Object)
                {
                    foreach (var nombre in new[] { "error", "mensaje", "message", "detail" })
                        foreach (var p in raiz.EnumerateObject())
                            if (p.Name.Equals(nombre, StringComparison.OrdinalIgnoreCase)
                                && p.Value.ValueKind == JsonValueKind.String
                                && !string.IsNullOrWhiteSpace(p.Value.GetString()))
                                return p.Value.GetString()!;

                    foreach (var p in raiz.EnumerateObject())
                    {
                        if (p.Name.Equals("errors", StringComparison.OrdinalIgnoreCase)
                            && p.Value.ValueKind == JsonValueKind.Object)
                        {
                            var msgs = new List<string>();
                            foreach (var campo in p.Value.EnumerateObject())
                                if (campo.Value.ValueKind == JsonValueKind.Array)
                                    foreach (var m in campo.Value.EnumerateArray())
                                        if (m.ValueKind == JsonValueKind.String) msgs.Add(m.GetString()!);
                            if (msgs.Count > 0) return string.Join(" ", msgs);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                if (cuerpo.Length < 200) return cuerpo;
            }
        }

        return estado switch
        {
            HttpStatusCode.Unauthorized => "Sesión vencida. Volvé a ingresar.",
            HttpStatusCode.Forbidden => "Solo un Admin puede gestionar usuarios.",
            HttpStatusCode.NotFound => "No se encontró el usuario.",
            _ => $"Error del servidor ({(int)estado})."
        };
    }
}