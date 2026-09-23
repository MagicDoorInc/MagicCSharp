using MagicCSharp.Data.Models;

namespace Acme.Libraries.Web.Models;

public record PaginationDto<T>
{
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
    public required IReadOnlyList<T> Items { get; init; }

    public static PaginationDto<T> From<TEntity>(Pagination<TEntity> pagination, Func<TEntity, T> toDto)
    {
        return new PaginationDto<T>
        {
            Page = pagination.Page,
            PageSize = pagination.PageSize,
            TotalCount = pagination.TotalCount,
            Items = pagination.Items.Select(toDto).ToList(),
        };
    }
}
