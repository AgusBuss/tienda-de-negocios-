using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InventarioApp.Models;

namespace InventarioApp.Services;

/// <summary>Resultado de cualquier llamada a la API: o trae datos, o trae un mensaje de error legible.</summary>
public class ApiResult<T>
{
    public bool Ok { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public int StatusCode { get; init; }
}

/// <summary>Respuesta del DELETE cuando hay baja lógica: { "mensaje": "..." }.</summary>
public class MensajeResponse
{
    public string? Mensaje { get; set; }
}

/// <summary>Respuesta de subir imagen: { "imagenUrl": "/uploads/..." }.</summary>
public class ImagenResponse
{
    public string? ImagenUrl { get; set; }
}

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly SessionService _sesion;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Rutas de la API (si alguna no coincide con tu Swagger, se corrige acá)
    private const string RutaLogin = "api/Auth/login";
    private const string RutaRegistro = "api/Auth/registro";
    private const string RutaProductos = "api/Productos";
    private const string RutaCategorias = "api/Categorias";
    private const string RutaCompras = "api/Compras";
    private const string RutaProveedores = "api/Proveedores";
    private const string RutaIngresos = "api/ingresos";
    private const string RutaClientes = "api/Clientes";
    private const string RutaSalidas = "api/Salidas";

    public ApiClient(HttpClient http, SessionService sesion)
    {
        _http = http;
        _sesion = sesion;
        _http.BaseAddress ??= new Uri(ApiConfig.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    // ---------- Auth ----------

    public async Task<ApiResult<AuthResponse>> LoginAsync(string usuario, string password)
    {
        var r = await EnviarAsync<AuthResponse>(HttpMethod.Post, RutaLogin,
            new LoginRequest { NombreUsuario = usuario, Password = password });

        if (r.Ok && r.Data is not null)
            await _sesion.GuardarSesionAsync(r.Data.Token);
        return r;
    }

    public async Task<ApiResult<AuthResponse>> RegistrarAsync(RegistroRequest req)
    {
        var r = await EnviarAsync<AuthResponse>(HttpMethod.Post, RutaRegistro, req);

        // Si la API devuelve token al registrar, se inicia sesión directamente.
        if (r.Ok && r.Data is not null && !string.IsNullOrEmpty(r.Data.Token))
            await _sesion.GuardarSesionAsync(r.Data.Token);
        return r;
    }

    // ---------- Catálogo ----------

    public Task<ApiResult<PagedResult<ProductoDto>>> GetProductosAsync(FiltroProductos f)
    {
        var q = new List<string>
        {
            $"pagina={f.Pagina}",
            $"tamanioPagina={f.TamanioPagina}",
            $"orden={Uri.EscapeDataString(f.Orden)}"
        };
        if (f.CategoriaId is > 0) q.Add($"categoriaId={f.CategoriaId}");
        if (!string.IsNullOrWhiteSpace(f.Buscar)) q.Add($"buscar={Uri.EscapeDataString(f.Buscar.Trim())}");
        if (f.SoloDisponibles) q.Add("soloDisponibles=true");
        if (f.PrecioMin is not null) q.Add($"precioMin={f.PrecioMin.Value.ToString(CultureInfo.InvariantCulture)}");
        if (f.PrecioMax is not null) q.Add($"precioMax={f.PrecioMax.Value.ToString(CultureInfo.InvariantCulture)}");
        if (f.IncluirInactivos) q.Add("incluirInactivos=true");

        return EnviarAsync<PagedResult<ProductoDto>>(HttpMethod.Get, $"{RutaProductos}?{string.Join("&", q)}");
    }

    public Task<ApiResult<ProductoDto>> GetProductoAsync(int id) =>
        EnviarAsync<ProductoDto>(HttpMethod.Get, $"{RutaProductos}/{id}");

    public async Task<ApiResult<List<CategoriaDto>>> GetCategoriasAsync()
    {
        // Acepta tanto una lista simple como un resultado paginado con "items".
        var r = await EnviarAsync<JsonElement>(HttpMethod.Get, RutaCategorias);
        if (!r.Ok)
            return new ApiResult<List<CategoriaDto>> { Ok = false, Error = r.Error, StatusCode = r.StatusCode };

        try
        {
            var el = r.Data;
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("items", out var items))
                el = items;
            var lista = el.Deserialize<List<CategoriaDto>>(JsonOpts) ?? new();
            return new ApiResult<List<CategoriaDto>> { Ok = true, Data = lista, StatusCode = r.StatusCode };
        }
        catch (Exception)
        {
            return new ApiResult<List<CategoriaDto>> { Ok = false, Error = "No se pudo leer la lista de categorías.", StatusCode = r.StatusCode };
        }
    }

    // ---------- Compras ----------

    public Task<ApiResult<CompraResultadoDto>> ComprarAsync(CompraCreateDto compra) =>
        EnviarAsync<CompraResultadoDto>(HttpMethod.Post, RutaCompras, compra);

    public Task<ApiResult<PagedResult<CompraResultadoDto>>> GetMisComprasAsync(int pagina = 1, int tamanio = 10) =>
        EnviarAsync<PagedResult<CompraResultadoDto>>(HttpMethod.Get,
            $"{RutaCompras}/mias?pagina={pagina}&tamanioPagina={tamanio}");

    public Task<ApiResult<CompraResultadoDto>> GetCompraAsync(int id) =>
        EnviarAsync<CompraResultadoDto>(HttpMethod.Get, $"{RutaCompras}/{id}");

    // ---------- Administración de productos (Admin / Vendedor) ----------

    public Task<ApiResult<ProductoDto>> CrearProductoAsync(ProductoCreateDto dto) =>
        EnviarAsync<ProductoDto>(HttpMethod.Post, RutaProductos, dto);

    // El PUT responde 204 sin cuerpo, por eso no se espera ningún dato de vuelta.
    public Task<ApiResult<object>> ActualizarProductoAsync(int id, ProductoCreateDto dto) =>
        EnviarAsync<object>(HttpMethod.Put, $"{RutaProductos}/{id}", dto);

    // Responde 204 si se borró, o 200 con { mensaje } si fue baja lógica (tiene historial).
    public Task<ApiResult<MensajeResponse>> EliminarProductoAsync(int id) =>
        EnviarAsync<MensajeResponse>(HttpMethod.Delete, $"{RutaProductos}/{id}");

    /// <summary>Sube la imagen como multipart/form-data con el campo "archivo".</summary>
    public async Task<ApiResult<ImagenResponse>> SubirImagenProductoAsync(int id, Stream contenido, string nombreArchivo)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var parte = new StreamContent(contenido);
            parte.Headers.ContentType = new MediaTypeHeaderValue(TipoDeImagen(nombreArchivo));
            form.Add(parte, "archivo", nombreArchivo);

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{RutaProductos}/{id}/imagen") { Content = form };
            if (_sesion.EstaLogueado)
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sesion.Token);

            using var resp = await _http.SendAsync(req);
            var texto = await resp.Content.ReadAsStringAsync();
            var codigo = (int)resp.StatusCode;

            if (resp.IsSuccessStatusCode)
            {
                var data = JsonSerializer.Deserialize<ImagenResponse>(texto, JsonOpts);
                return new ApiResult<ImagenResponse> { Ok = true, Data = data, StatusCode = codigo };
            }

            if (resp.StatusCode == HttpStatusCode.Unauthorized && _sesion.EstaLogueado)
                _sesion.CerrarSesion();

            return new ApiResult<ImagenResponse> { Ok = false, StatusCode = codigo, Error = ExtraerError(texto, resp.StatusCode) };
        }
        catch (TaskCanceledException)
        {
            return new ApiResult<ImagenResponse> { Ok = false, Error = "El servidor tardó demasiado en responder." };
        }
        catch (HttpRequestException)
        {
            return new ApiResult<ImagenResponse> { Ok = false, Error = "No se pudo conectar con el servidor." };
        }
        catch (JsonException)
        {
            return new ApiResult<ImagenResponse> { Ok = false, Error = "La respuesta del servidor no tiene el formato esperado." };
        }
    }

    private static string TipoDeImagen(string nombreArchivo) =>
        Path.GetExtension(nombreArchivo).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

    // ---------- Proveedores e ingresos de mercadería (Admin / Vendedor) ----------

    public Task<ApiResult<List<ProveedorDto>>> GetProveedoresAsync() =>
        GetListaAsync<ProveedorDto>(RutaProveedores);

    public Task<ApiResult<List<IngresoDto>>> GetIngresosAsync() =>
        GetListaAsync<IngresoDto>(RutaIngresos);

    public Task<ApiResult<IngresoDto>> CrearIngresoAsync(IngresoCreateDto dto) =>
        EnviarAsync<IngresoDto>(HttpMethod.Post, RutaIngresos, dto);

    // ---------- Clientes y ventas de mostrador (Admin / Vendedor) ----------

    public Task<ApiResult<List<ClienteDto>>> GetClientesAsync() =>
        GetListaAsync<ClienteDto>(RutaClientes);

    public Task<ApiResult<ClienteDto>> CrearClienteAsync(ClienteCreateDto dto) =>
        EnviarAsync<ClienteDto>(HttpMethod.Post, RutaClientes, dto);

    public Task<ApiResult<List<SalidaDto>>> GetSalidasAsync() =>
        GetListaAsync<SalidaDto>(RutaSalidas);

    public Task<ApiResult<SalidaDto>> CrearSalidaAsync(SalidaCreateDto dto) =>
        EnviarAsync<SalidaDto>(HttpMethod.Post, RutaSalidas, dto);

    /// <summary>Lee una lista que puede venir como arreglo simple o como objeto paginado con "items".</summary>
    private async Task<ApiResult<List<T>>> GetListaAsync<T>(string ruta)
    {
        var r = await EnviarAsync<JsonElement>(HttpMethod.Get, ruta);
        if (!r.Ok)
            return new ApiResult<List<T>> { Ok = false, Error = r.Error, StatusCode = r.StatusCode };

        try
        {
            var el = r.Data;
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

    // ---------- Núcleo: arma el pedido, agrega el token y traduce errores ----------

    private async Task<ApiResult<T>> EnviarAsync<T>(HttpMethod metodo, string ruta, object? cuerpo = null)
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
            {
                if (string.IsNullOrWhiteSpace(texto))
                    return new ApiResult<T> { Ok = true, StatusCode = codigo };

                var data = JsonSerializer.Deserialize<T>(texto, JsonOpts);
                return new ApiResult<T> { Ok = true, Data = data, StatusCode = codigo };
            }

            // Token vencido o inválido estando logueado: se cierra la sesión.
            if (resp.StatusCode == HttpStatusCode.Unauthorized && _sesion.EstaLogueado)
                _sesion.CerrarSesion();

            return new ApiResult<T> { Ok = false, StatusCode = codigo, Error = ExtraerError(texto, resp.StatusCode) };
        }
        catch (TaskCanceledException)
        {
            return new ApiResult<T> { Ok = false, Error = "El servidor tardó demasiado en responder." };
        }
        catch (HttpRequestException)
        {
            return new ApiResult<T> { Ok = false, Error = "No se pudo conectar con el servidor. Revisá la conexión y que la API esté en línea." };
        }
        catch (JsonException)
        {
            return new ApiResult<T> { Ok = false, Error = "La respuesta del servidor no tiene el formato esperado." };
        }
    }

    /// <summary>Busca el mensaje de error en los formatos que devuelve la API (error, mensaje, ProblemDetails, validaciones).</summary>
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

                    // Errores de validación: { "errors": { "Campo": ["mensaje", ...] } }
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
                // No era JSON: si es texto corto, se usa tal cual.
                if (cuerpo.Length < 200) return cuerpo;
            }
        }

        return estado switch
        {
            HttpStatusCode.Unauthorized => "Credenciales inválidas o sesión vencida.",
            HttpStatusCode.Forbidden => "No tenés permiso para realizar esta acción.",
            HttpStatusCode.NotFound => "No se encontró lo que buscabas.",
            HttpStatusCode.PaymentRequired => "El pago fue rechazado.",
            HttpStatusCode.ServiceUnavailable => "El servicio no está disponible en este momento.",
            _ => $"Error del servidor ({(int)estado})."
        };
    }
}