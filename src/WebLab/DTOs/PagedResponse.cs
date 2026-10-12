namespace WebLab.DTOs;

public class PagedResponse<T>
{
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    public int TotalCount { get; set; }
    public int Skip { get; set; }
    public int Limit { get; set; }
    public string? NextLink { get; set; }
}
