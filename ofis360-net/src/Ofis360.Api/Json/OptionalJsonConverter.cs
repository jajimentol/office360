using System.Text.Json;
using System.Text.Json.Serialization;
using Ofis360.Application.Common;

namespace Ofis360.Api.Json;

/// <summary>
/// Optional&lt;T&gt;: JSON'da alan yoksa HasValue=false (dokunma), alan null ise HasValue=true/Value=null (NULL'a cek).
/// System.Text.Json eksik alanlari hic set etmedigi icin default(Optional) = "gonderilmedi" anlamina gelir.
/// </summary>
public sealed class OptionalJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var inner = typeToConvert.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(typeof(OptionalJsonConverter<>).MakeGenericType(inner))!;
    }
}

public sealed class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
{
    public override bool HandleNull => true;

    public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return Optional<T>.Of(default);
        return Optional<T>.Of(JsonSerializer.Deserialize<T>(ref reader, options));
    }

    public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
    {
        if (!value.HasValue || value.Value is null) writer.WriteNullValue();
        else JsonSerializer.Serialize(writer, value.Value, options);
    }
}
