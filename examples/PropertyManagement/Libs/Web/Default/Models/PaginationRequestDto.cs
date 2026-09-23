using MagicCSharp.Data.Models;

namespace Acme.Libraries.Web.Models;

public record PaginationRequestDto
{
    public int? Page { get; init; }
    public int? PageSize { get; init; }

    public PaginationRequest ToRequest()
    {
        return new PaginationRequest
        {
            Page = Page ?? 1,
            PageSize = PageSize ?? 50,
        };
    }
}
