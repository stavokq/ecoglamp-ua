using System.ComponentModel.DataAnnotations;

namespace WebLab.Models;

public class Activity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Назва активності є обов'язковою")]
    [StringLength(100)]
    [Display(Name = "Назва активності / зручності")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Іконка (Bootstrap Icon class)")]
    [StringLength(50)]
    public string IconClass { get; set; } = "bi-tree";

    public ICollection<GlampingActivity> GlampingActivities { get; set; } = new List<GlampingActivity>();
}
