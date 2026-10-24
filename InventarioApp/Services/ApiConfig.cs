namespace InventarioApp.Services;

public static class ApiConfig
{
    // true = usa la API publicada en MonsterASP, false = usa la API local
    public const bool UsarProduccion = false;

    private const string UrlProduccion = "https://inventarioapitrani.tryasp.net";
    private const string UrlLocalWindows = "https://localhost:7295";
    private const string UrlLocalAndroid = "https://10.0.2.2:7295"; // 10.0.2.2 = la PC vista desde el emulador

    public static string BaseUrl
    {
        get
        {
            if (UsarProduccion) return UrlProduccion;
#if ANDROID
            return UrlLocalAndroid;
#else
            return UrlLocalWindows;
#endif
        }
    }

    /// <summary>
    /// Las imágenes de productos vienen como "/uploads/xxx.jpg".
    /// Esto les antepone la base de la API para poder mostrarlas.
    /// </summary>
    public static string? ResolverUrl(string? rutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa)) return null;
        if (rutaRelativa.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return rutaRelativa;
        return BaseUrl.TrimEnd('/') + "/" + rutaRelativa.TrimStart('/');
    }
}