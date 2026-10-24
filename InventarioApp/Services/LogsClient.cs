using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using InventarioApp.Models;

namespace InventarioApp.Services;

/// <summary>Consulta de logs de errores y auditoría (solo Admin).</summary>
public class LogsClient
{
    private const string RutaErrores = "api/logs/errores";
    private const string RutaAuditoria = "api/logs/auditoria";

    private readonly HttpClient _http;
    private readonly SessionService _sesion;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public LogsClient(HttpClient http, SessionService sesion)
    {
        _http = http;
        _sesion = sesion;
        _http.BaseAddress ??= new Uri(ApiConfig.BaseUrl.TrimEnd('/') + "/");
    }

    public Task<ApiResult<PagedResult<LogErrorDto>>> GetErroresAsync(int pagina = 1, int tamanio = 20) =>
        GetPaginadoAsync<LogErrorDto>(RutaErrores, pagina, tamanio);

    public Task<ApiResult<PagedResult<AuditoriaDto>>> GetAuditoriaAsync(int pagina = 1, int tamanio = 20) =>
        GetPaginadoAsync<AuditoriaDto>(RutaAuditoria, pagina, tamanio);

    /// <summary>Acepta tanto un resultado paginado como un arreglo simple.</summary>
    private async Task<ApiResult<PagedResult<T>>> GetPaginadoAsync<T>(string ruta, int pagina, int tamanio)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{ruta}?pagina={pagina}&tamanioPagina={tamanio}");
            if (_sesion.EstaLogueado)
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sesion.Token);

            using var resp = await _http.SendAsync(req);
            var texto = await resp.Content.ReadAsStringAsync();
            var codigo = (int)resp.StatusCode;

            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == HttpStatusCode.Unauthorized && _sesion.EstaLogueado)
                    _sesion.CerrarSesion();

                return new ApiResult<PagedResult<T>> { Ok = false, StatusCode = codigo, Error = MensajeDeError(resp.StatusCode) };
            }

            using var doc = JsonDocument.Parse(texto);
            var el = doc.RootElement;

            PagedResult<T> resultado;
            if (el.ValueKind == JsonValueKind.Array)
            {
                var lista = el.Deserialize<List<T>>(JsonOpts) ?? new();
                resultado = new PagedResult<T>
                {
                    Items = lista,
                    PaginaActual = 1,
                    TamanioPagina = lista.Count,
                    TotalRegistros = lista.Count,
                    TotalPaginas = 1
                };
            }
            else
            {
                resultado = el.Deserialize<PagedResult<T>>(JsonOpts) ?? new();
            }

            return new ApiResult<PagedResult<T>> { Ok = true, Data = resultado, StatusCode = codigo };
        }
        catch (TaskCanceledException)
        {
            return new ApiResult<PagedResult<T>> { Ok = false, Error = "El servidor tardó demasiado en responder." };
        }
        catch (HttpRequestException)
        {
            return new ApiResult<PagedResult<T>> { Ok = false, Error = "No se pudo conectar con el servidor." };
        }
        catch (JsonException)
        {
            return new ApiResult<PagedResult<T>> { Ok = false, Error = "La respuesta del servidor no tiene el formato esperado." };
        }
    }

    private static string MensajeDeError(HttpStatusCode estado) => estado switch
    {
        HttpStatusCode.Unauthorized => "Sesión vencida. Volvé a ingresar.",
        HttpStatusCode.Forbidden => "Solo un Admin puede ver los logs.",
        _ => $"Error del servidor ({(int)estado})."
    };
}