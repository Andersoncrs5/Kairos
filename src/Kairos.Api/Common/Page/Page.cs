namespace Kairos.Api.Common.Page;

public sealed class Page<T>
{
    public IReadOnlyList<T> Data { get; }
    public int Count => Data.Count;
    public long TotalCount { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public long TotalPages => TotalCount == 0 ? 0 : ((TotalCount - 1) / PageSize) + 1;
    public long Offset => (long)(PageNumber - 1) * PageSize;
    public bool HasData => Count > 0;
    public bool IsEmpty => Count == 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public bool IsFirstPage => PageNumber == 1;
    public bool IsLastPage => PageNumber >= TotalPages;
    public long? FirstItemIndex => IsEmpty ? null : Offset + 1;
    public long? LastItemIndex => IsEmpty ? null : Math.Min(Offset + Count, TotalCount);

    private Page(
        IReadOnlyList<T> data,
        long totalCount,
        int pageNumber,
        int pageSize)
    {
        Data = data;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    public static Page<T> Create(
        IEnumerable<T> data,
        long totalCount,
        int pageNumber,
        int pageSize)
    {
        ArgumentNullException.ThrowIfNull(data);

        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        T[] items = data.ToArray();

        return new Page<T>(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
}