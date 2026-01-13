using System.Net.Http.Json;

namespace Tracking.Web.Services
{
    public interface IAlertService
    {
        Task SendCriticalAlertAsync(string message, Exception? exception = null);
    }

    public class AlertService : IAlertService
    {
        private readonly ILogger<AlertService> _logger;
        private readonly IConfiguration _configuration;

        public AlertService(ILogger<AlertService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task SendCriticalAlertAsync(string message, Exception? exception = null)
        {
            // 1) Logar sempre
            _logger.LogCritical(exception, "🚨 ALERTA CRÍTICO: {Message}", message);

            // 2) Enviar para webhook (Slack, Teams, etc) - OPCIONAL
            var webhookUrl = _configuration["Alerts:WebhookUrl"];
            if (!string.IsNullOrEmpty(webhookUrl))
            {
                try
                {
                    using var httpClient = new HttpClient();
                    var payload = new
                    {
                        text = $"🚨 **TrackingWeb ALERTA**\n{message}",
                        exception = exception?.ToString()
                    };

                    await httpClient.PostAsJsonAsync(webhookUrl, payload);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falha ao enviar alerta para webhook");
                }
            }

            // 3) Adicionar mais canais (e-mail, SMS, etc) aqui no futuro
        }
    }
}