using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

public class JsonDateOnlyConverter : JsonConverter<DateOnly>
{
    private readonly string _format;
    public JsonDateOnlyConverter(string format = "dd/MM/yyyy") => _format = format;

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (DateOnly.TryParseExact(value, _format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        throw new JsonException($"Ngày không hợp lệ. Định dạng đúng: {_format}");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(_format));
}
