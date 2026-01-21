using System.Text.Json.Serialization;

namespace Tracking.Web.Models
{
    // ========== Request/Response para API Interna ==========
    
    // Request de autenticação
    public record AuthRequest(
        [property: JsonPropertyName("identifier")] string Identifier
    );

    // Response de autenticação
    public class AuthResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_at")]
        public DateTime ExpiresAt { get; set; }
    }

    // Request de rastreio
    public record RastreioRequest(
        [property: JsonPropertyName("identificador")] string Identificador
    );

    // Response completa da API interna
    public class InternalTrackingResponse
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        [JsonPropertyName("cpf")]
        public string? Cpf { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("cdRastreio")]
        public string? CdRastreio { get; set; }

        [JsonPropertyName("prediction")]
        public DateTime? Prediction { get; set; }

        [JsonPropertyName("eventos")]
        public List<TimelineEvent>? Eventos { get; set; }
    }

    public class TimelineEvent
    {
        [JsonPropertyName("idTimeline")]
        public int IdTimeline { get; set; }

        [JsonPropertyName("statusTimeline")]
        public string? statusTimeline { get; set; }

        [JsonPropertyName("dsTimeline")]
        public string? dsTimeline { get; set; }

        [JsonPropertyName("final")]
        public DateTime? final { get; set; }
    }

    // ========== Request do Frontend para o Backend Blazor ==========

    public record TrackRequest(string Code, string? Token);

    // ========== Configurações ==========
    
    public class TrackingOptions
    {
        public string? ApiBaseUrl { get; set; }
        public string? TurnstileSecret { get; set; }
        public bool RequireTurnstile { get; set; } = true;
        public RateLimitOptions RateLimit { get; set; } = new();
    }

    public class RateLimitOptions
    {
        public int PerIpLimit { get; set; } = 30;
        public int PerIpWindowSeconds { get; set; } = 60;
        public int PerCodeLimit { get; set; } = 6;
        public int PerCodeWindowSeconds { get; set; } = 60;
    }

    // ========== Resposta Normalizada para o Frontend ==========
    
    public class FrontendTrackingResponse
    {
        public string? OrderNumber { get; set; }
        public string? OrderDate { get; set; }
        public string? PredictionDate { get; set; }
        public string? IdErp { get; set; }
        public string? Message { get; set; }
        public List<FrontendEvent> Events { get; set; } = new();
    }

    public class FrontendEvent
    {
        public string Date { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Complement { get; set; }
        public int? InternalCode { get; set; }
    }
}