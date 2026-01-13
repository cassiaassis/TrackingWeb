using System.Net.Http.Json;
using System.Text.Json;
using Tracking.Web.Models;

namespace Tracking.Web.Services
{
    /// <summary>
    /// Cliente HTTP para comunicação com API de rastreamento
    /// </summary>
    public interface IApiClient
    {
        Task<FrontendTrackingResponse?> TrackOrderAsync(string code, string? turnstileToken = null);
    }

    public class ApiClient : IApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiClient> _logger;

        public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<FrontendTrackingResponse?> TrackOrderAsync(string code, string? turnstileToken = null)
        {
            _logger.LogInformation("🔵 ApiClient.TrackOrderAsync chamado com code: {Code}", code); // 👈 ADICIONAR
            
            try
            {
                var request = new TrackRequest(code, turnstileToken);
                
                _logger.LogInformation("🔵 Fazendo POST para /api/track"); // 👈 ADICIONAR

                var response = await _httpClient.PostAsJsonAsync("/api/track", request);
                
                _logger.LogInformation("🔵 Response Status: {Status}", response.StatusCode); // 👈 ADICIONAR

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("Rate limit atingido ao consultar código {Code}", code);
                    throw new RateLimitExceededException("Muitas tentativas. Aguarde um momento.");
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Código não encontrado: {Code}", code);
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("API retornou {Status}: {Content}", response.StatusCode, errorContent);
                    throw new ApiException($"Erro ao consultar rastreamento: {response.StatusCode}");
                }

                var result = await response.Content.ReadFromJsonAsync<FrontendTrackingResponse>();
                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro de rede ao consultar código {Code}", code);
                throw new ApiException("Erro de conexão. Verifique sua internet.", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erro ao processar resposta da API para código {Code}", code);
                throw new ApiException("Erro ao processar resposta.", ex);
            }
        }
    }

    // Exceções customizadas
    public class ApiException : Exception
    {
        public ApiException(string message) : base(message) { }
        public ApiException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class RateLimitExceededException : Exception
    {
        public RateLimitExceededException(string message) : base(message) { }
    }
}