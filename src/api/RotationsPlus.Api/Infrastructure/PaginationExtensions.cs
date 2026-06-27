using Microsoft.EntityFrameworkCore;
using RotationsPlus.Contracts.Common;

namespace RotationsPlus.Api.Infrastructure;

/// <summary>
/// Server-side pagination helpers for list endpoints. Endpoints normalise the caller's page/pageSize with
/// <see cref="Normalize"/> (defaults + a hard cap so a client can't ask for an unbounded page), then page
/// their final projected query with <see cref="ToPagedResponseAsync{T}"/>, which runs a COUNT for the total
/// and a Skip/Take for the page in two queries.
/// </summary>
public static class PaginationExtensions
{
    /// <summary>Default page size when the caller doesn't specify one (matches the admin tables' page size).</summary>
    public const int DefaultPageSize = 10;

    /// <summary>Hard ceiling on a single page so a caller can't request an unbounded read.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Hard ceiling on a free-text search term (cheap DoS guard); mirrors the program catalog search.</summary>
    public const int MaxSearchLength = 100;

    /// <summary>Clamps a caller's page/pageSize to sane bounds: page ≥ 1, 1 ≤ pageSize ≤ <see cref="MaxPageSize"/>.</summary>
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var p = page is { } pv && pv > 0 ? pv : 1;
        var size = pageSize is { } sv && sv > 0 ? Math.Min(sv, MaxPageSize) : DefaultPageSize;
        return (p, size);
    }

    /// <summary>
    /// Validates and turns a free-text search term into a `%…%` ILIKE pattern with the literal wildcards
    /// (<c>\ % _</c>) escaped, so caller input matches literally and can't inject pattern metacharacters.
    /// Returns false (with <paramref name="error"/>) if the term exceeds <see cref="MaxSearchLength"/>;
    /// a null/blank term yields a null pattern (no search filter) and true.
    /// </summary>
    public static bool TryBuildSearchPattern(string? q, out string? pattern, out string? error)
    {
        pattern = null;
        error = null;
        if (string.IsNullOrWhiteSpace(q))
        {
            return true;
        }

        var term = q.Trim();
        if (term.Length > MaxSearchLength)
        {
            error = $"q must be {MaxSearchLength} characters or fewer.";
            return false;
        }

        pattern = EscapeLike(term);
        return true;
    }

    /// <summary>Escapes the ILIKE wildcards (<c>\ % _</c>) in a raw term and wraps it as a `%…%` contains
    /// pattern. Use for building a secondary pattern (e.g. a stripped rotation number) from raw input —
    /// never slice an already-built pattern by offset.</summary>
    public static string EscapeLike(string term)
    {
        var escaped = term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return $"%{escaped}%";
    }

    /// <summary>
    /// Materialises one page of <paramref name="query"/>: a COUNT of the full (filtered) set for the total,
    /// then the page's rows via Skip/Take. Apply ordering and the DTO projection to <paramref name="query"/>
    /// before calling — a stable order is required for correct paging.
    /// </summary>
    public static async Task<PagedResponse<T>> ToPagedResponseAsync<T>(
        this IQueryable<T> query, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = Normalize(page, pageSize);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((p - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
        return new PagedResponse<T>(items, p, size, total);
    }
}
