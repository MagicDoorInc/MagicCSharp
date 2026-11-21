namespace MagicCSharp.Clients;

internal static class RequestPaths
{
    /// <summary>The path before its query string, which can hold personal values a message must not carry.</summary>
    public static string WithoutQuery(string path)
    {
        var queryStart = path.IndexOf('?');
        return queryStart < 0 ? path : path.Substring(0, queryStart);
    }
}
