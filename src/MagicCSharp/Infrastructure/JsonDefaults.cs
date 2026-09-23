using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MagicCSharp.Infrastructure;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        // TODO: add DataTimeOffsetConverter to ensure that DateTimeOffset is serialized as a UTC string
        Converters =
        {
            new JsonStringEnumConverter(),
            new OptionalConverterFactory(),
        },
        // PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IgnoreReadOnlyFields = true,
        IgnoreReadOnlyProperties = true,
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        DefaultBufferSize = 4096,
        // Postgres jsonb does not preserve property order, so a polymorphic type's "$type" discriminator can come
        // back anywhere in the object rather than first. Without this, deserializing such a value throws.
        AllowOutOfOrderMetadataProperties = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                IgnorePropertyRequired,
                SkipAbsentOptionals,
            },
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

    /// <summary>
    ///     Omit <see cref="Optional{T}" /> properties that hold no value.
    ///     <para>
    ///         The converter for an absent optional writes nothing, which after a property name is malformed JSON.
    ///         Deciding here, before the property name is written, is the only place that can skip it entirely —
    ///         and skipping is the right output: an absent optional means "the caller did not mention this".
    ///     </para>
    /// </summary>
    private static void SkipAbsentOptionals(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (var propertyInfo in typeInfo.Properties)
        {
            if (!typeof(IOptional).IsAssignableFrom(propertyInfo.PropertyType))
            {
                continue;
            }

            propertyInfo.ShouldSerialize = (_, value) => value is IOptional { HasValue: true };
        }
    }
}
