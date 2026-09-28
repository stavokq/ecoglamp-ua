using System.ComponentModel.DataAnnotations;

namespace WebLab.Models;

public class Region
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Назва регіону є обов'язковою")]
    [StringLength(100, ErrorMessage = "Назва не може перевищувати 100 символів")]
    [Display(Name = "Назва регіону")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Опис регіону")]
    [StringLength(500)]
    public string? Description { get; set; }

    public ICollection<GlampingSite> GlampingSites { get; set; } = new List<GlampingSite>();
}
