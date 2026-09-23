using System.Text.Json;
using System.Text.Json.Serialization;

namespace MagicCSharp.Infrastructure;

/// <summary>
///     Makes <see cref="Optional{T}" /> round-trip through System.Text.Json. Register it in
///     <c>JsonSerializerOptions.Converters</c>; <see cref="JsonDefaults" /> already does.
/// </summary>
public class OptionalConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(typeof(OptionalConverter<>).MakeGenericType(valueType))!;
    }
}
