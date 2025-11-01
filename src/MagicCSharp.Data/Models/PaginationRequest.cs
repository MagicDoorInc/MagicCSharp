namespace MagicCSharp.Data.Models;

/// <summary>
/// Represents a request for paginated data.
/// </summary>
public record PaginationRequest
{
    private int page = 1;
    private int pageSize = 50;

    /// <summary>
    /// Creates a pagination request.
    /// </summary>
    /// <param name="disable">If true, pagination is disabled and all items are returned.</param>
    public PaginationRequest(bool disable = false)
    {
        Disable = disable;
    }

    /// <summary>
    /// Creates a pagination request with specific page size and page number.
    /// </summary>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="page">Page number (1-based).</param>
    public PaginationRequest(int pageSize, int page = 1)
    {
        this.pageSize = pageSize;
        this.page = page;
    }

    /// <summary>
    /// If true, pagination is disabled and all items are returned.
    /// </summary>
    public bool Disable { get; set; }

    /// <summary>
    /// Current page number (1-based). Minimum value is 1.
    /// </summary>
    public int Page
    {
        get
        {
            if (page < 1)
            {
                return 1;
            }

            return page;
        }
        set => page = value;
    }

    /// <summary>
    /// Number of items per page. Minimum value is 1, default is 50.
    /// </summary>
    public int PageSize
    {
        get
        {
            if (pageSize < 1)
            {
                return 50;
            }

            return pageSize;
        }
        set => pageSize = value;
    }

    /// <summary>
    /// Number of items to skip (calculated from page and page size).
    /// </summary>
    public int Skip => PageSize * (Page - 1);
}
