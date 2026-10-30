using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InventarioApp.Models;

namespace InventarioApp.Services;

/// <summary>Alta, edición y baja de categorías, proveedores y clientes (Admin y Vendedor).</summary>
public class MaestrosClient
{
    private const string RutaCategorias = "api/Categorias";
    private const string RutaProveedores = "api/Proveedores";
    private const string RutaClientes = "api/Clientes";

    private readonly HttpClient _http;
    private readonly SessionService _sesion;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public MaestrosClient(HttpClient http, SessionService sesion)
    {
        _http = http;
        _sesion = sesion;
        _http.BaseAddress ??= new Uri(ApiConfig.BaseUrl.TrimEnd('/') + "/");
    }

    // ---------- Categorías ----------
    public Task<ApiResult<List<CategoriaDto>>> GetCategoriasAsync() => ListarAsync<CategoriaDto>(RutaCategorias);
    public Task<ApiResult<string>> CrearCategoriaAsync(CategoriaCreateDto dto) => EnviarAsync(HttpMethod.Post, RutaCategorias, dto);
    public Task<ApiResult<string>> ActualizarCategoriaAsync(int id, CategoriaCreateDto dto) => EnviarAsync(HttpMethod.Put, $"{RutaCategorias}/{id}", dto);
    public Task<ApiResult<string>> EliminarCategoriaAsync(int id) => EnviarAsync(HttpMethod.Delete, $"{RutaCategorias}/{id}", null);

    // ---------- Proveedores ----------
    public Task<ApiResult<List<ProveedorDto>>> GetProveedoresAsync() => ListarAsync<ProveedorDto>(RutaProveedores);
    public Task<ApiResult<string>> CrearProveedorAsync(ProveedorCreateDto dto) => EnviarAsync(HttpMethod.Post, RutaProveedores, dto);
    public Task<ApiResult<string>> ActualizarProveedorAsync(int id, ProveedorCreateDto dto) => EnviarAsync(HttpMethod.Put, $"{RutaProveedores}/{id}", dto);
    public Task<ApiResult<string>> EliminarProveedorAsync(int id) => EnviarAsync(HttpMethod.Delete, $"{RutaProveedores}/{id}", null);

    // ---------- Clientes ----------
    public Task<ApiResult<List<ClienteDto>>> GetClientesAsync() => ListarAsync<ClienteDto>(RutaClientes);
    public Task<ApiResult<string>> CrearClienteAsync(ClienteCreateDto dto) => EnviarAsync(HttpMethod.Post, RutaClientes, dto);
    public Task<ApiResult<string>> ActualizarClienteAsync(int id, ClienteCreateDto dto) => EnviarAsync(HttpMethod.Put, $"{RutaClientes}/{id}", dto);
    public Task<ApiResult<string>> EliminarClienteAsync(int id) => EnviarAsync(HttpMethod.Delete, $"{RutaClientes}/{id}", null);

    // ---------- Núcleo ----------

    /// <summary>Trae hasta 100 registros; acepta resultado paginado con "items" o arreglo simple.</summary>
    private async Task<ApiResult<List<T>>> ListarAsync<T>(string ruta)
    {
        var r = await EnviarAsync(HttpMethod.Get, $"{ruta}?pagina=1&tamanioPagina=100", null);
        if (!r.Ok)
            return new ApiResult<List<T>> { Ok = false, Error = r.Error, StatusCode = r.StatusCode };

        try
        {
            using var doc = JsonDocument.Parse(r.Data ?? "[]");
            var el = doc.RootElement;
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("items", out var items))
                el = items;
            var lista = el.Deserialize<List<T>>(JsonOpts) ?? new();
            return new ApiResult<List<T>> { Ok = true, Data = lista, StatusCode = r.StatusCode };
        }
        catch (Exception)
        {
            return new ApiResult<List<T>> { Ok = false, Error = "No se pudo leer la lista recibida.", StatusCode = r.StatusCode };
        }
    }

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
            HttpStatusCode.Forbidden => "No tenés permiso para esta acción.",
            HttpStatusCode.NotFound => "No se encontró el registro.",
            _ => $"Error del servidor ({(int)estado})."
        };
    }
}