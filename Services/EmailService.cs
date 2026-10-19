using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace InventarioAPI.Services
{
    // Se llena desde la sección "Email" del appsettings
    public class EmailSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Puerto { get; set; } = 587;
        public string Usuario { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string RemitenteNombre { get; set; } = "Tienda de Negocios";
        public string RemitenteEmail { get; set; } = string.Empty;
    }

    // Se usa cuando el correo no está configurado o el servidor lo rechaza
    public class EmailException : Exception
    {
        public EmailException(string mensaje, Exception? inner = null) : base(mensaje, inner) { }
    }

    public class EmailService
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task EnviarConAdjuntoAsync(
            string destinatario,
            string asunto,
            string cuerpo,
            byte[] adjunto,
            string nombreAdjunto)
        {
            if (string.IsNullOrWhiteSpace(_settings.Host) ||
                string.IsNullOrWhiteSpace(_settings.Usuario) ||
                string.IsNullOrWhiteSpace(_settings.Password))
            {
                throw new EmailException("El envío de correos no está configurado en el servidor.");
            }

            if (!MailboxAddress.TryParse(destinatario, out var para))
                throw new EmailException("La dirección de correo del destinatario no es válida.");

            var remitenteEmail = string.IsNullOrWhiteSpace(_settings.RemitenteEmail)
                ? _settings.Usuario
                : _settings.RemitenteEmail;

            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(_settings.RemitenteNombre, remitenteEmail));
            mensaje.To.Add(para);
            mensaje.Subject = asunto;

            var builder = new BodyBuilder { TextBody = cuerpo };
            builder.Attachments.Add(nombreAdjunto, adjunto, new ContentType("application", "pdf"));
            mensaje.Body = builder.ToMessageBody();

            try
            {
                using var cliente = new SmtpClient();
                cliente.Timeout = 20000; // 20 segundos

                // Puerto 465 = SSL directo; 587 = STARTTLS
                var seguridad = _settings.Puerto == 465
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls;

                await cliente.ConnectAsync(_settings.Host, _settings.Puerto, seguridad);
                await cliente.AuthenticateAsync(_settings.Usuario, _settings.Password);
                await cliente.SendAsync(mensaje);
                await cliente.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // El detalle técnico queda en el log; al usuario le llega un mensaje genérico
                _logger.LogError(ex, "No se pudo enviar el correo a {Destinatario}", destinatario);
                throw new EmailException("No se pudo enviar el correo. Revisá la configuración del servidor de correo.", ex);
            }
        }
    }
}