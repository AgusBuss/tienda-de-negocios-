using InventarioApp.Services;
using Microsoft.Extensions.Logging;

namespace InventarioApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

            // Servicios propios de la app
            builder.Services.AddSingleton<SessionService>();
            builder.Services.AddSingleton<HttpClient>();
            builder.Services.AddSingleton<ApiClient>();
            builder.Services.AddSingleton<CarritoService>();
            builder.Services.AddSingleton<ReportesClient>();
            builder.Services.AddSingleton<UsuariosClient>();
            builder.Services.AddSingleton<LogsClient>();
            builder.Services.AddSingleton<MaestrosClient>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}