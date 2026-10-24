using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace InventarioApp.Services;

/// <summary>Descarga reportes PDF de la API y pide su envío por correo.</summary>
public class ReportesClient
{
    private readonly HttpClient _http;
    private readonly SessionService _sesion;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ReportesClient(HttpClient http, SessionService sesion)
    {
        _http = http;
        _sesion = sesion;
        _http.BaseAddress ??= new Uri(ApiConfig.BaseUrl.TrimEnd('/') + "/");
    }

    /// <summary>Descarga el PDF y lo guarda en la carpeta temporal del dispositivo. Devuelve la ruta del archivo.</summary>
    public async Task<(bool Ok, string? Ruta, string? Error)> DescargarAsync(string tipo, DateTime? desde, DateTime? hasta)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, ConstruirRuta(tipo, desde, hasta));
            AgregarToken(req);

            using var resp = await _http.SendAsync(req);

            if (!resp.IsSuccessStatusCode)
            {
                var texto = await resp.Content.ReadAsStringAsync();
                return (false, null, ExtraerError(texto, resp.StatusCode));
            }

            var bytes = await resp.Content.ReadAsByteArrayAsync();
            var ruta = Path.Combine(FileSystem.CacheDirectory, $"reporte-{tipo}-{DateTime.Now:yyyyMMdd-HHmmss}.pdf");
            await File.WriteAllBytesAsync(ruta, bytes);
            return (true, ruta, null);
        }
        catch (TaskCanceledException)
        {
            return (false, null, "El servidor tardó demasiado en responder.");
        }
        catch (HttpRequestException)
        {
            return (false, null, "No se pudo conectar con el servidor.");
        }
        catch (IOException)
        {
            return (false, null, "No se pudo guardar el archivo en el dispositivo.");
        }
    }

    /// <summary>Abre el PDF con la app del sistema; si no se puede, ofrece compartirlo.</summary>
    public async Task AbrirAsync(string ruta)
    {
        try
        {
            await Launcher.Default.OpenAsync(new OpenFileRequest("Reporte", new ReadOnlyFile(ruta)));
        }
        catch (Exception)
        {
            await Share.Default.RequestAsync(new ShareFileRequest("Reporte", new ShareFile(ruta)));
        }
    }

    /// <summary>Pide a la API que envíe el reporte por correo (si no hay destinatario, usa el email del usuario).</summary>
    public async Task<(bool Ok, string? Mensaje)> EnviarPorCorreoAsync(string tipo, DateTime? desde, DateTime? hasta, string? destinatario)
    {
        try
        {
            var cuerpo = new
            {
                tipo,
                desde = tipo == "stock" ? null : desde,
                hasta = tipo == "stock" ? null : hasta,
                destinatario = string.IsNullOrWhiteSpace(destinatario) ? null : destinatario.Trim()
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "api/Reportes/enviar")
            {
                Content = new StringContent(JsonSerializer.Serialize(cuerpo, JsonOpts), Encoding.UTF8, "application/json")
            };
            AgregarToken(req);

            using var resp = await _http.SendAsync(req);
            var texto = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
                return (true, ExtraerMensaje(texto) ?? "El reporte fue enviado por correo.");

            if (resp.StatusCode == HttpStatusCode.Unauthorized && _sesion.EstaLogueado)
                _sesion.CerrarSesion();

            return (false, ExtraerError(texto, resp.StatusCode));
        }
        catch (TaskCanceledException)
        {
            return (false, "El servidor tardó demasiado en responder.");
        }
        catch (HttpRequestException)
        {
            return (false, "No se pudo conectar con el servidor.");
        }
    }

    // ---------- Auxiliares ----------

    private static string ConstruirRuta(string tipo, DateTime? desde, DateTime? hasta)
    {
        var ruta = $"api/Reportes/{tipo}";
        if (tipo == "stock") return ruta;

        var q = new List<string>();
        if (desde is not null) q.Add($"desde={desde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (hasta is not null) q.Add($"hasta={hasta.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        return q.Count == 0 ? ruta : $"{ruta}?{string.Join("&", q)}";
    }

    private void AgregarToken(HttpRequestMessage req)
    {
        if (_sesion.EstaLogueado)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sesion.Token);
    }

    private static string? ExtraerMensaje(string cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo)) return null;
        try
        {
            using var doc = JsonDocument.Parse(cuerpo);
            if (doc.RootElement.ValueKind == JsonValueKind.String) return doc.RootElement.GetString();
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                foreach (var p in doc.RootElement.EnumerateObject())
                    if ((p.Name.Equals("mensaje", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Equals("message", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Equals("error", StringComparison.OrdinalIgnoreCase))
                        && p.Value.ValueKind == JsonValueKind.String)
                        return p.Value.GetString();
        }
        catch (JsonException)
        {
            if (cuerpo.Length < 200) return cuerpo;
        }
        return null;
    }

    private static string ExtraerError(string cuerpo, HttpStatusCode estado) =>
        ExtraerMensaje(cuerpo) ?? estado switch
        {
            HttpStatusCode.Unauthorized => "Sesión vencida. Volvé a ingresar.",
            HttpStatusCode.Forbidden => "No tenés permiso para ver reportes.",
            HttpStatusCode.ServiceUnavailable => "El correo no está configurado o no se pudo enviar.",
            _ => $"Error del servidor ({(int)estado})."
        };
}