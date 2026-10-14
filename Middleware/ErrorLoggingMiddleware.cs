using System.Security.Claims;
using InventarioAPI.Data;
using InventarioAPI.Models;

namespace InventarioAPI.Middleware
{
    public class ErrorLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ErrorLoggingMiddleware> _logger;

        public ErrorLoggingMiddleware(
            RequestDelegate next,
            IServiceScopeFactory scopeFactory,
            ILogger<ErrorLoggingMiddleware> logger)
        {
            _next = next;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no controlado en {Metodo} {Ruta}",
                    context.Request.Method, context.Request.Path);

                await GuardarLogAsync(context, ex);

                // Si la respuesta ya empezó a enviarse no se puede modificar
                if (context.Response.HasStarted) throw;

                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Ocurrió un error inesperado. El incidente quedó registrado."
                });
            }
        }

        private async Task GuardarLogAsync(HttpContext context, Exception ex)
        {
            try
            {
                // Scope nuevo: el DbContext de la petición puede haber quedado en mal estado
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                int? usuarioId = null;
                var claim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(claim, out var id)) usuarioId = id;

                var mensaje = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                var ruta = context.Request.Path.Value;
                if (ruta != null && ruta.Length > 300) ruta = ruta[..300];

                db.LogErrores.Add(new LogError
                {
                    Fecha = DateTime.Now,
                    Mensaje = mensaje,
                    StackTrace = ex.ToString(),
                    Ruta = ruta,
                    Metodo = context.Request.Method,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    UsuarioId = usuarioId
                });

                await db.SaveChangesAsync();
            }
            catch (Exception logEx)
            {
                // Si falla el guardado del log, no tiene que romper la respuesta
                _logger.LogError(logEx, "No se pudo guardar el log de error en la base.");
            }
        }
    }
}