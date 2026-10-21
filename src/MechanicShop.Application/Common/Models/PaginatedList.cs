namespace MechanicShop.Application.Common.Models;

/// <summary>
/// List page + paging metadata. Paged endpoints return this so the client can render
/// "page X of Y" and request the next page without guessing.
/// </summary>
public sealed record PaginatedList<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
