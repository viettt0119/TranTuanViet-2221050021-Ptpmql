using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Models;

public sealed class PagedList<T>
{
    public static IReadOnlyList<int> PageSizeOptions { get; } = [10, 20, 50];

    public IReadOnlyList<T> Items { get; }
    public int PageIndex { get; }
    public int PageSize { get; }
    public int TotalRecords { get; }
    public int TotalPages { get; }
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;

    private PagedList(IReadOnlyList<T> items, int pageIndex, int pageSize, int totalRecords)
    {
        Items = items;
        PageIndex = pageIndex;
        PageSize = pageSize;
        TotalRecords = totalRecords;
        TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
    }

    public static async Task<PagedList<T>> CreateAsync(
        IQueryable<T> source,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        pageSize = PageSizeOptions.Contains(pageSize) ? pageSize : PageSizeOptions[0];
        int totalRecords = await source.CountAsync(cancellationToken);
        int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        pageIndex = totalPages == 0 ? 1 : Math.Clamp(pageIndex, 1, totalPages);

        var items = await source
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<T>(items, pageIndex, pageSize, totalRecords);
    }
}
