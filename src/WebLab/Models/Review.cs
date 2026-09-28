using System.ComponentModel.DataAnnotations;

namespace WebLab.Models;

public class Review
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ім'я автора є обов'язковим")]
    [StringLength(100)]
    [Display(Name = "Ім'я автора")]
    public string AuthorName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть оцінку")]
    [Range(1, 5, ErrorMessage = "Оцінка має бути від 1 до 5")]
    [Display(Name = "Оцінка (1-5)")]
    public int Rating { get; set; }

    [Required(ErrorMessage = "Текст відгуку є обов'язковим")]
    [StringLength(1000)]
    [Display(Name = "Відгук")]
    public string Comment { get; set; } = string.Empty;

    [Display(Name = "Дата")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int GlampingSiteId { get; set; }
    public GlampingSite? GlampingSite { get; set; }
}
