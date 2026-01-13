using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tracking.Web.Models
{
    /// <summary>
    /// Conversor JSON flexível para DateTime que aceita múltiplos formatos
    /// </summary>
    public class DateTimeFlexibleConverter : JsonConverter<DateTime?>
    {
        private static readonly string[] Formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",           // Formato padrão
            "yyyy-MM-dd HH:mm:ss.ffffff",    // Com microssegundos
            "yyyy-MM-dd'T'HH:mm:ss",         // ISO 8601 básico
            "yyyy-MM-dd'T'HH:mm:ss.ffffff",  // ISO 8601 com microssegundos
            "dd/MM/yyyy HH:mm:ss",           // Formato brasileiro
        };

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                var dateString = reader.GetString();
                
                if (string.IsNullOrWhiteSpace(dateString))
                    return null;

                // Tenta parsear com cada formato
                foreach (var format in Formats)
                {
                    if (DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        return date;
                    }
                }

                // Fallback: tenta parse genérico
                if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var genericDate))
                {
                    return genericDate;
                }

                // Se falhar tudo, retorna null ao invés de exceção
                return null;
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
