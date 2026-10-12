using System.ComponentModel.DataAnnotations;

namespace WebLab.DTOs;

public class RegionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SitesCount { get; set; }
}

public class CreateRegionDto
{
    [Required(ErrorMessage = "Назва регіону є обов'язковою")]
    [StringLength(100, ErrorMessage = "Назва не може перевищувати 100 символів")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}
