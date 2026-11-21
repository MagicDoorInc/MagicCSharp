using System.Collections;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

namespace MagicCSharp.Clients;

/// <summary>
///     The base of a generated client when no <see cref="MagicClientConventionAttribute" /> names another: JSON over
///     the named <see cref="HttpClient" /> that <see cref="MagicHttpClientRegistration.AddMagicHttpClient{TInterface,TClient}" />
///     configures for the concrete client type.
/// </summary>
public abstract class MagicHttpClientBase(IHttpClientFactory httpClientFactory)
{
    /// <summary>Sends the request and reads its JSON answer; an empty answer (a 204) is <c>default</c>.</summary>
    protected async Task<TResponse> SendRequestAsync<TResponse>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var httpResponseMessage = await SendAsync(method, path, body, cancellationToken);
        var content = await httpResponseMessage.Content.ReadAsByteArrayAsync(cancellationToken);
        if (content.Length == 0)
        {
            return EmptyAnswer<TResponse>(method, path);
        }

        return JsonSerializer.Deserialize<TResponse>(content, JsonSerializerOptions.Web)!;
    }

    /// <summary>Sends the request and ignores any answer.</summary>
    protected async Task SendRequestAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var httpResponseMessage = await SendAsync(method, path, body, cancellationToken);
    }

    /// <summary>
    ///     Appends the public properties of each value as <c>name=value</c> pairs; a list repeats its name, a null is
    ///     left out. Values are written in the invariant culture, dates in round-trip format.
    /// </summary>
    protected static string WithQuery(string path, params object?[] values)
    {
        var queryPairs = values.Where(value => value != null).SelectMany(value => QueryPairs(value!)).ToList();
        if (queryPairs.Count == 0)
        {
            return path;
        }

        var separator = path.Contains('?') ? "&" : "?";
        return path + separator + string.Join("&", queryPairs);
    }

    /// <summary>True when the service answered 404, which a client method returning <c>T?</c> turns into null.</summary>
    protected static bool IsNotFound(Exception exception)
    {
        var httpRequestException = exception as HttpRequestException;
        return httpRequestException != null && httpRequestException.StatusCode == HttpStatusCode.NotFound;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var httpRequestMessage = new HttpRequestMessage(method, path);
        if (body != null)
        {
            httpRequestMessage.Content = JsonContent.Create(body, body.GetType(), options: JsonSerializerOptions.Web);
        }

        var httpClient = httpClientFactory.CreateClient(GetType().Name);
        var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, cancellationToken);
        if (httpResponseMessage.IsSuccessStatusCode)
        {
            return httpResponseMessage;
        }

        using (httpResponseMessage)
        {
            var responseBody = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken);
            throw new MagicHttpClientException(method, path, httpResponseMessage.StatusCode, responseBody);
        }
    }

    // A null answer cannot be told apart from "no answer" for a reference type, so both are null; a value type
    // the caller did not make nullable has nothing to stand for "no answer".
    private static TResponse EmptyAnswer<TResponse>(HttpMethod method, string path)
    {
        var responseType = typeof(TResponse);
        if (responseType.IsValueType && Nullable.GetUnderlyingType(responseType) == null)
        {
            throw new InvalidOperationException($"{method} {path} answered with no content, which is not a {responseType.Name}.");
        }

        return default!;
    }

    private static IEnumerable<string> QueryPairs(object value)
    {
        var properties = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.GetIndexParameters().Length == 0);
        foreach (var property in properties)
        {
            var propertyValue = property.GetValue(value);
            foreach (var queryValue in QueryValues(value, property.Name, propertyValue))
            {
                yield return Uri.EscapeDataString(property.Name) + "=" + Uri.EscapeDataString(queryValue);
            }
        }
    }

    private static IEnumerable<string> QueryValues(object value, string propertyName, object? propertyValue)
    {
        if (propertyValue == null)
        {
            return [];
        }

        var text = QueryText(propertyValue);
        if (text != null)
        {
            return [text];
        }

        var items = propertyValue as IEnumerable;
        if (items == null)
        {
            throw NotAQueryValue(value, propertyName);
        }

        return items.Cast<object?>().Where(item => item != null).Select(item => QueryText(item!) ?? throw NotAQueryValue(value, propertyName));
    }

    private static NotSupportedException NotAQueryValue(object value, string propertyName)
    {
        return new NotSupportedException(
            $"{value.GetType().Name}.{propertyName} holds an object, which a query string cannot; send it as the request body or write the client method by hand.");
    }

    private static string? QueryText(object value)
    {
        var textValue = value as string;
        if (textValue != null)
        {
            return textValue;
        }

        if (value is bool)
        {
            return (bool)value ? "true" : "false";
        }

        if (value is DateTimeOffset)
        {
            return ((DateTimeOffset)value).ToString("O", CultureInfo.InvariantCulture);
        }

        if (value is DateTime)
        {
            return ((DateTime)value).ToString("O", CultureInfo.InvariantCulture);
        }

        if (value is Enum)
        {
            return value.ToString();
        }

        var formattable = value as IFormattable;
        if (formattable != null)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return null;
    }
}
