using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PentiaCase.Web.Serialization;

internal sealed class WingineersDateTimeConverter : JsonConverter<DateTime>
{
    private const string Format = "dd-MM-yyyy HH:mm";
    private static readonly string[] ReadFormats = { Format, "yyyy-MM-dd HH:mm" };

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String &&
            DateTime.TryParseExact(reader.GetString(), ReadFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        throw new JsonException($"Expected a date and time in {string.Join(" or ", ReadFormats)} format.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
    }
}
