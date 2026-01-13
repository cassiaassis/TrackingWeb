namespace Tracking.Web.Services
{
    /// <summary>
    /// Serviço para controlar Rate Limiting (Limitação de Taxa de Requisições)
    /// Previne abuso controlando quantas vezes um IP ou código pode fazer requests
    /// </summary>
    public interface IRateLimitService
    {
        /// <summary>
        /// Tenta incrementar contador de requisições para uma chave específica
        /// </summary>
        /// <param name="key">Chave de identificação (ex: "ip:192.168.1.1" ou "code:32676652800")</param>
        /// <param name="limit">Limite máximo de requisições permitidas</param>
        /// <param name="windowSeconds">Janela de tempo em segundos para contagem</param>
        /// <returns>True se ainda está dentro do limite, False se excedeu</returns>
        Task<bool> TryIncrementAsync(string key, int limit, int windowSeconds);

        /// <summary>
        /// Obtém quantidade atual de requisições para uma chave
        /// </summary>
        Task<int> GetCurrentCountAsync(string key);

        /// <summary>
        /// Reseta contador para uma chave específica (útil para testes ou liberação manual)
        /// </summary>
        Task ResetAsync(string key);
    }
}