using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MagicCSharp.Infrastructure;

/// <summary>
///     How MagicCSharp's own types go over HTTP, in one place for both ends: a MagicApp server adds
///     <see cref="AddConverters" /> to its controller and minimal-API options, and the default HTTP client in
///     MagicCSharp.Clients reads and writes with <see cref="HttpClientOptions" />, so the two agree.
///     <para>
///         Enums travel as their name, as <c>MagicDbContext</c> stores them: inserting a member renumbers everything
///         after it, and a caller that hard-coded <c>2</c> is then wrong in a way nothing reports.
///         <see cref="Optional{T}" /> round-trips, so a PATCH body can tell "set this to null" apart from "do not
///         touch this".
///     </para>
/// </summary>
public static class MagicJsonConventions
{
    /// <summary>
    ///     The web defaults ASP.NET Core serializes with (camelCase names, case-insensitive reading, numbers read from
    ///     strings) plus <see cref="AddConverters" />. An <see cref="Optional{T}" /> that holds no value is left out of
    ///     what it writes, which is how a body says "do not touch this". Read-only.
    /// </summary>
    public static JsonSerializerOptions HttpClientOptions { get; } = CreateHttpClientOptions();

    /// <summary>
    ///     Adds the enum-name and <see cref="Optional{T}" /> converters, each unless one of its type is already there, so
    ///     calling this twice — or after registering your own — does not stack duplicates or override a deliberate
    ///     choice.
    /// </summary>
    /// <param name="options">Options that are not read-only yet.</param>
    public static void AddConverters(JsonSerializerOptions options)
    {
        if (!options.Converters.Any(converter => converter is JsonStringEnumConverter))
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }

        if (!options.Converters.Any(converter => converter is OptionalConverterFactory))
        {
            options.Converters.Add(new OptionalConverterFactory());
        }
    }

    /// <summary>
    ///     Omits <see cref="Optional{T}" /> properties that hold no value.
    ///     <para>
    ///         The converter for an absent optional writes nothing, which after a property name is malformed JSON.
    ///         Deciding here, before the property name is written, is the only place that can skip it entirely —
    ///         and skipping is the right output: an absent optional means "the caller did not mention this".
    ///     </para>
    /// </summary>
    internal static void SkipAbsentOptionals(JsonTypeInfo typeInfo)
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

            propertyInfo.ShouldSerialize = (_, value) => value is IOptional && ((IOptional)value).HasValue;
        }
    }

    private static JsonSerializerOptions CreateHttpClientOptions()
    {
        var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    SkipAbsentOptionals,
                },
            },
        };
        AddConverters(jsonSerializerOptions);
        jsonSerializerOptions.MakeReadOnly();

        return jsonSerializerOptions;
    }
}
