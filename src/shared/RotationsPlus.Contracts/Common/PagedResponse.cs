namespace RotationsPlus.Contracts.Common;

/// <summary>
/// One page of a server-paginated list. <see cref="Items"/> is the current page; <see cref="TotalCount"/>
/// is the total number of rows matching the query (across all pages, after filtering) so the client can
/// render "showing X–Y of N" and page controls without fetching everything.
/// </summary>
/// <typeparam name="T">The item/DTO type.</typeparam>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    /// <summary>Total number of pages for the current <see cref="PageSize"/> (at least 1).</summary>
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
}
