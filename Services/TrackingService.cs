using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Tracking.Web.Models;

namespace Tracking.Web.Services
{
    public interface ITrackingService
    {
        Task<InternalTrackingResponse?> GetTrackingAsync(string cpfOrEmail, CancellationToken ct = default);
        Task<bool> VerifyTurnstileAsync(string token, string remoteIp, CancellationToken ct = default);
    }

    public class TrackingService : ITrackingService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IMemoryCache _cache;
        private readonly TrackingOptions _opts;
        private readonly ILogger<TrackingService> _logger;

        private const string TOKEN_CACHE_PREFIX = "jwt_token:";

        public TrackingService(
            IHttpClientFactory httpFactory,
            IMemoryCache cache,
            IOptions<TrackingOptions> opts,
            ILogger<TrackingService> logger)
        {
            _httpFactory = httpFactory;
            _cache = cache;
            _opts = opts.Value;
            _logger = logger;
        }

        public async Task<InternalTrackingResponse?> GetTrackingAsync(string cpfOrEmail, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(cpfOrEmail))
            {
                _logger.LogWarning("Identificador vazio fornecido para GetTrackingAsync");
                return null;
            }

            try
            {
                var token = await GetValidTokenAsync(cpfOrEmail, ct);
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogError("Falha ao obter token JWT para {Identifier}", MaskIdentifier(cpfOrEmail));
                    return null;
                }

                var response = await ConsultarRastreioAsync(cpfOrEmail, token, ct);

                // 👇 IMPORTANTE: NÃO RENOVAR TOKEN PARA 404 (CPF não encontrado)
                if (response == null)
                {
                    _logger.LogInformation("Sem retry - response já tratada ou erro definitivo");
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter rastreio para {Identifier}", MaskIdentifier(cpfOrEmail));
                return null;
            }
        }

        private async Task<string?> GetValidTokenAsync(string identifier, CancellationToken ct)
        {
            var cacheKey = TOKEN_CACHE_PREFIX + identifier;

            if (_cache.TryGetValue<string>(cacheKey, out var cachedToken))
            {
                _logger.LogDebug("Token encontrado em cache para {Identifier}", MaskIdentifier(identifier));
                return cachedToken;
            }

            _logger.LogInformation("🔐 Autenticando para obter novo token JWT para {Identifier}", MaskIdentifier(identifier));
            
            var client = _httpFactory.CreateClient("internalApi");
            var authRequest = new AuthRequest(identifier);

            try
            {
                var baseUrl = client.BaseAddress?.ToString().TrimEnd('/') ?? "";
                var authPath = "/api/auth/authenticate";
                var fullUrl = baseUrl + authPath;
                
                _logger.LogInformation("🔵 URL de autenticação: {Url}", fullUrl);
                
                var response = await client.PostAsJsonAsync(authPath, authRequest, ct);

                _logger.LogInformation("🔵 Status da autenticação: {Status}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("❌ Autenticação falhou com status {Status} para {Identifier}. Erro: {Error}", 
                        response.StatusCode, MaskIdentifier(identifier), errorContent);
                    return null;
                }

                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
                if (authResponse == null || string.IsNullOrEmpty(authResponse.AccessToken))
                {
                    _logger.LogError("⚠️ Response de autenticação inválida para {Identifier}", MaskIdentifier(identifier));
                    return null;
                }

                // ✅ DIAGNÓSTICO DE FUSO HORÁRIO
                var nowUtc = DateTime.UtcNow;
                var nowLocal = DateTime.Now;
                var serverExpiration = authResponse.ExpiresAt;

                _logger.LogInformation("🕐 Diagnóstico de tempo:");
                _logger.LogInformation("   - Servidor (UTC): {ServerTime}", nowUtc);
                _logger.LogInformation("   - Local: {LocalTime}", nowLocal);
                _logger.LogInformation("   - Token expira em: {ExpiresAt} (Kind: {Kind})", 
                    serverExpiration, serverExpiration.Kind);
                _logger.LogInformation("   - Diferença: {Diff} minutos", 
                    (serverExpiration - nowUtc).TotalMinutes);

                // ✅ VALIDAR SE TOKEN JÁ ESTÁ EXPIRADO
                if (serverExpiration <= nowUtc)
                {
                    _logger.LogError("❌ Token JÁ ESTÁ EXPIRADO ao ser recebido!");
                    _logger.LogError("   - ExpiresAt: {ExpiresAt}", serverExpiration);
                    _logger.LogError("   - Agora UTC: {Now}", nowUtc);
                    return null;
                }

                var expiresIn = serverExpiration - nowUtc;

                if (expiresIn.TotalSeconds < 30)
                {
                    _logger.LogWarning("⚠️ Token expira em menos de 30 segundos! ({Seconds}s)", 
                        expiresIn.TotalSeconds);
                }

                var cacheExpiration = expiresIn.Subtract(TimeSpan.FromMinutes(1));
                _cache.Set(cacheKey, authResponse.AccessToken, cacheExpiration);
                
                _logger.LogInformation("✅ Token JWT armazenado em cache por {Minutes} minutos", 
                    cacheExpiration.TotalMinutes);

                return authResponse.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Erro ao autenticar para {Identifier}", MaskIdentifier(identifier));
                return null;
            }
        }

        private async Task<InternalTrackingResponse?> ConsultarRastreioAsync(
            string identifier,
            string token,
            CancellationToken ct)
        {
            var client = _httpFactory.CreateClient("internalApi");

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/rastreio");

            // Headers
            request.Headers.Add("Authorization", $"Bearer {token}");
            request.Headers.Add("Accept", "application/json");

            // Corpo correto: { "identificador": "<cpf ou email>" }
            var body = new { identificador = identifier };
            request.Content = JsonContent.Create(body);

            try
            {
                var baseUrl = client.BaseAddress?.ToString().TrimEnd('/') ?? "";
                var fullUrl = baseUrl + "/api/rastreio";

                _logger.LogInformation("🔵 Enviando request para {Url}", fullUrl);
                _logger.LogInformation("🔵 Authorization Header: Bearer {TokenPreview}...",
                    token.Length > 20 ? token.Substring(0, 20) : token);

                var response = await client.SendAsync(request, ct);

                _logger.LogInformation("🔵 Response Status: {Status}", response.StatusCode);

                // 👇 TRATAMENTO 404 - CPF NÃO ENCONTRADO
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogInformation("ℹ️ CPF/Email não encontrado. Response: {Content}", errorContent);

                    string message = "CPF ou e-mail não localizado.";

                    try
                    {
                        using var errorDoc = JsonDocument.Parse(errorContent);
                        if (errorDoc.RootElement.TryGetProperty("message", out var messageElement))
                        {
                            message = messageElement.GetString() ?? message;
                        }
                    }
                    catch (JsonException)
                    {
                        // Ignorar erro de parsing
                    }

                    return new InternalTrackingResponse
                    {
                        Message = message
                        // Preencha outros campos se necessário
                    };
                }

                // 👇 TRATAMENTO 401 - TOKEN INVÁLIDO
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("🔒 401 Unauthorized! Response: {Content}", errorContent);

                    if (response.Headers.WwwAuthenticate.Any())
                    {
                        _logger.LogError("   - WWW-Authenticate: {Auth}",
                            string.Join(", ", response.Headers.WwwAuthenticate));
                    }

                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("❌ API retornou {Status}. Response: {Content}",
                        response.StatusCode, errorContent);
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogInformation("🔵 JSON Recebido (primeiros 500 chars): {Json}",
                    jsonContent.Length > 500 ? jsonContent.Substring(0, 500) + "..." : jsonContent);

                try
                {
                    var trackingResponse = await JsonSerializer.DeserializeAsync<InternalTrackingResponse>(
                        new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonContent)),
                        cancellationToken: ct);

                    if (trackingResponse != null)
                    {
                        _logger.LogInformation("✅ Rastreio deserializado: CPF={Cpf}, Email={Email}, Eventos={Count}",
                            trackingResponse.Cpf, trackingResponse.Email, trackingResponse.Eventos?.Count ?? 0);
                    }

                    return trackingResponse;
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "💥 Erro ao deserializar JSON: {Json}", jsonContent);
                    return null;
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "🌐 Erro de rede ao consultar rastreio");
                return null;
            }
            catch (TaskCanceledException timeoutEx)
            {
                _logger.LogError(timeoutEx, "⏱️ Timeout ao consultar rastreio");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Erro inesperado ao consultar rastreio");
                return null;
            }
        }


        public async Task<bool> VerifyTurnstileAsync(string token, string remoteIp, CancellationToken ct = default)
        {
            if (!_opts.RequireTurnstile)
                return true;

            if (string.IsNullOrWhiteSpace(_opts.TurnstileSecret))
            {
                _logger.LogWarning("TurnstileSecret não configurado mas RequireTurnstile=true");
                return false;
            }

            try
            {
                var client = _httpFactory.CreateClient("turnstile");
                var form = new Dictionary<string, string>
                {
                    ["secret"] = _opts.TurnstileSecret!,
                    ["response"] = token ?? string.Empty,
                    ["remoteip"] = remoteIp ?? string.Empty
                };

                var response = await client.PostAsync(
                    "https://challenges.cloudflare.com/turnstile/v0/siteverify",
                    new FormUrlEncodedContent(form),
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Turnstile verify retornou {Status}", response.StatusCode);
                    return false;
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    _logger.LogDebug("Turnstile verificado com sucesso para IP {IP}", remoteIp);
                    return true;
                }

                _logger.LogWarning("Turnstile falhou para IP {IP}", remoteIp);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar Turnstile");
                return false;
            }
        }

        private static string MaskIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return "****";
            
            if (identifier.Contains('@'))
            {
                var parts = identifier.Split('@');
                if (parts.Length == 2 && parts[0].Length > 2)
                {
                    return $"{parts[0][0]}***{parts[0][^1]}@{parts[1]}";
                }
            }
            
            if (identifier.Length >= 4 && identifier.All(char.IsDigit))
            {
                return new string('*', identifier.Length - 2) + identifier[^2..];
            }

            return "****";
        }
    }
}