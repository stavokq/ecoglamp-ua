namespace WebLab.ViewModels;

public class RegionStatItem
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public double AvgPrice { get; set; }
}

public class ActivityStatItem
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class AnalyticsViewModel
{
    public int TotalSites { get; set; }
    public int TotalReviews { get; set; }
    public double AvgPriceTotal { get; set; }
    public int Capacity12 { get; set; }
    public int Capacity34 { get; set; }
    public int Capacity5Plus { get; set; }
    public List<RegionStatItem> Regions { get; set; } = new();
    public List<ActivityStatItem> Activities { get; set; } = new();
}
