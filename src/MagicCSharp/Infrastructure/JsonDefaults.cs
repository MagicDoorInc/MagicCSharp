using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MagicCSharp.Infrastructure;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        // TODO: add DataTimeOffsetConverter to ensure that DateTimeOffset is serialized as a UTC string
        Converters = { new JsonStringEnumConverter() },
        // PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IgnoreReadOnlyFields = true,
        IgnoreReadOnlyProperties = true,
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        DefaultBufferSize = 4096,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { IgnorePropertyRequired },
        },
    };

    // make all properties not required, so that we can deserialize without them
    private static void IgnorePropertyRequired(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind == JsonTypeInfoKind.Object)
        {
            foreach (var propertyInfo in typeInfo.Properties)
            {
                propertyInfo.IsRequired = false;
            }
        }
    }
}