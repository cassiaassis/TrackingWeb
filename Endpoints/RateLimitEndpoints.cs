using Tracking.Web.Services;

namespace Tracking.Web.Endpoints
{
    public static class RateLimitEndpoints
    {
        /// <summary>
        /// Endpoints para monitorar/gerenciar Rate Limiting
        /// ATENÇÃO: Remover ou proteger em PRODUÇÃO!
        /// </summary>
        public static void MapRateLimitEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/ratelimit")
                .WithTags("RateLimit - Diagnóstico");

            // GET /api/ratelimit/check/{key}
            // Verifica contagem atual sem incrementar
            group.MapGet("/check/{key}", async (
                string key,
                IRateLimitService rateLimitService) =>
            {
                var count = await rateLimitService.GetCurrentCountAsync(key);
                return Results.Ok(new
                {
                    key,
                    currentCount = count,
                    timestamp = DateTimeOffset.UtcNow
                });
            })
            .WithName("CheckRateLimit")
            .WithSummary("Consulta contagem atual de requisições para uma chave");

            // POST /api/ratelimit/reset/{key}
            // Reseta contador manualmente
            group.MapPost("/reset/{key}", async (
                string key,
                IRateLimitService rateLimitService,
                ILogger<Program> logger) =>
            {
                await rateLimitService.ResetAsync(key);
                logger.LogWarning("Rate limit resetado manualmente para chave {Key}", key);
                
                return Results.Ok(new
                {
                    message = $"Rate limit resetado para {key}",
                    timestamp = DateTimeOffset.UtcNow
                });
            })
            .WithName("ResetRateLimit")
            .WithSummary("Reseta contador de rate limit (use apenas em DEV!)");
        }
    }
}