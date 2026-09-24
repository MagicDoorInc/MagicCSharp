namespace MagicCSharp.Data.Models;

/// <summary>
///     Represents a paginated result set.
/// </summary>
/// <typeparam name="T">The type of items in the result set.</typeparam>
public record Pagination<T>
{
    /// <summary>
    ///     Creates an empty pagination result.
    /// </summary>
    public Pagination()
    {
        Page = 1;
        PageSize = 0;
        TotalCount = 0;
        Items = [];
    }

    /// <summary>
    ///     Creates a pagination result with the specified values.
    /// </summary>
    public Pagination(
        int page,
        int pageSize,
        int totalCount,
        IReadOnlyList<T> items) : this()
    {
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
        Items = items;
    }

    /// <summary>
    ///     Creates a pagination result from a request and total count.
    /// </summary>
    public Pagination(PaginationRequest paginationRequest, int totalCount, IReadOnlyList<T> items)
    {
        Page = paginationRequest.Page;
        PageSize = paginationRequest.PageSize;
        TotalCount = totalCount;
        Items = items;
    }

    /// <summary>
    ///     Current page number (1-based).
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    ///     Number of items per page.
    /// </summary>
    public int PageSize { get; init; } = 10;

    /// <summary>
    ///     Total number of items across all pages.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    ///     Items in the current page.
    /// </summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>
    ///     Total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}