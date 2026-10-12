namespace WebLab.DTOs;

public class GlampingSiteDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public decimal PricePerNight { get; set; }
    public int MaxGuests { get; set; }
    public string? ImageUrl { get; set; }
    public string? DocumentUrl { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime CreatedAt { get; set; }
    public int RegionId { get; set; }
    public string? RegionName { get; set; }
    public List<int> ActivityIds { get; set; } = new();
    public List<string> ActivityNames { get; set; } = new();
    public double AverageRating { get; set; }
    public int ReviewsCount { get; set; }
}
