using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace Tracking.Web.Services
{
    /// <summary>
    /// Implementação de Rate Limiting usando IMemoryCache
    /// Armazena timestamps (marcas de tempo) de requisições em memória
    /// </summary>
    public class MemoryRateLimitService : IRateLimitService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<MemoryRateLimitService> _logger;

        // ConcurrentDictionary: Dicionário thread-safe (suporta múltiplos acessos simultâneos)
        // Armazena lista de timestamps de requisições por chave
        private const string RATE_LIMIT_PREFIX = "ratelimit:";

        public MemoryRateLimitService(IMemoryCache cache, ILogger<MemoryRateLimitService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Implementa Sliding Window (Janela Deslizante) para contagem precisa
        /// </summary>
        public Task<bool> TryIncrementAsync(string key, int limit, int windowSeconds)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogWarning("Chave vazia fornecida para TryIncrementAsync");
                return Task.FromResult(false);
            }

            var cacheKey = RATE_LIMIT_PREFIX + key;
            var now = DateTimeOffset.UtcNow;
            var windowStart = now.AddSeconds(-windowSeconds);

            // Obtém ou cria lista de timestamps
            var timestamps = _cache.GetOrCreate(cacheKey, entry =>
            {
                // Define tempo de expiração do cache (janela + margem de segurança)
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(windowSeconds + 60);
                return new ConcurrentBag<DateTimeOffset>();
            });

            if (timestamps == null)
            {
                _logger.LogError("Falha ao criar/obter timestamps para chave {Key}", key);
                return Task.FromResult(false);
            }

            // Remove timestamps antigos (fora da janela de tempo)
            // ConcurrentBag não permite remoção, então recriamos a lista filtrada
            var validTimestamps = timestamps.Where(ts => ts >= windowStart).ToList();
            
            // Verifica se já atingiu o limite
            if (validTimestamps.Count >= limit)
            {
                _logger.LogWarning("Rate limit excedido para {Key}: {Count}/{Limit} em {Window}s",
                    key, validTimestamps.Count, limit, windowSeconds);
                return Task.FromResult(false);
            }

            // Adiciona novo timestamp e atualiza cache
            validTimestamps.Add(now);
            var newBag = new ConcurrentBag<DateTimeOffset>(validTimestamps);
            
            _cache.Set(cacheKey, newBag, TimeSpan.FromSeconds(windowSeconds + 60));

            _logger.LogDebug("Rate limit OK para {Key}: {Count}/{Limit} em {Window}s",
                key, validTimestamps.Count + 1, limit, windowSeconds);

            return Task.FromResult(true);
        }

        /// <summary>
        /// Retorna contagem atual de requisições na janela de tempo
        /// </summary>
        public Task<int> GetCurrentCountAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return Task.FromResult(0);

            var cacheKey = RATE_LIMIT_PREFIX + key;
            var timestamps = _cache.Get<ConcurrentBag<DateTimeOffset>>(cacheKey);

            if (timestamps == null || timestamps.IsEmpty)
                return Task.FromResult(0);

            // Considera apenas timestamps ainda válidos
            var windowStart = DateTimeOffset.UtcNow.AddSeconds(-60); // Janela padrão de 60s
            var count = timestamps.Count(ts => ts >= windowStart);

            return Task.FromResult(count);
        }

        /// <summary>
        /// Remove todos os timestamps de uma chave (reset manual)
        /// </summary>
        public Task ResetAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return Task.CompletedTask;

            var cacheKey = RATE_LIMIT_PREFIX + key;
            _cache.Remove(cacheKey);

            _logger.LogInformation("Rate limit resetado para chave {Key}", key);
            return Task.CompletedTask;
        }
    }
}