using System.ComponentModel.DataAnnotations;

namespace Acme.Libraries.Web.Models;

/// <summary>
///     Snowflake ids cross the JSON boundary as strings, because JavaScript numbers lose precision above
///     2^53. These turn them back into longs, rejecting anything that is not one with a 400.
/// </summary>
public static class Parser
{
    public static long ToId(this string id)
    {
        if (!long.TryParse(id, out var parsedId))
        {
            throw new ValidationException($"'{id}' is not a valid id.");
        }

        return parsedId;
    }

    public static List<long> ToIds(this IEnumerable<string> ids)
    {
        return ids.Select(ToId).ToList();
    }
}
