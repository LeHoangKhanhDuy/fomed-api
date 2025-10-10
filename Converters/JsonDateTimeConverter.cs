using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

public class JsonDateTimeConverter : JsonConverter<DateTime>
{
    private readonly string _format;
    public JsonDateTimeConverter(string format = "dd/MM/yyyy") => _format = format;

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (DateTime.TryParseExact(value, _format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        throw new JsonException($"Ngày không hợp lệ. Định dạng đúng: {_format}");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(_format));
}
