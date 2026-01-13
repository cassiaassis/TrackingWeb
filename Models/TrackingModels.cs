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
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("info")]
        public OrderInfo? Info { get; set; }

        [JsonPropertyName("shippingevents")]
        public List<ShippingEvent> ShippingEvents { get; set; } = new();
    }

    // Informações do pedido
    public class OrderInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("number")]
        public string Number { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("prediction")]
        public string Prediction { get; set; } = string.Empty;

        [JsonPropertyName("iderp")]
        public string? IdErp { get; set; }
    }

    // Evento de rastreio
    public class ShippingEvent
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("dscode")]
        public string DsCode { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("detalhe")]
        public string Detalhe { get; set; } = string.Empty;

        [JsonPropertyName("complement")]
        public string? Complement { get; set; }

        [JsonPropertyName("dtshipping")]
        [JsonConverter(typeof(DateTimeFlexibleConverter))] // 👈 ADICIONAR ESTA LINHA
        public DateTime? DtShipping { get; set; }

        [JsonPropertyName("internalcode")]
        public int? InternalCode { get; set; }
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