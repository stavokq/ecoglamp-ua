using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebLab.Models;

public class GlampingSite
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Назва глемпінгу є обов'язковою")]
    [StringLength(150, ErrorMessage = "Назва не може перевищувати 150 символів")]
    [Display(Name = "Назва глемпінгу")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Опис є обов'язковим")]
    [Display(Name = "Опис")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Адреса є обов'язковою")]
    [StringLength(200)]
    [Display(Name = "Адреса / Локація")]
    public string Address { get; set; } = string.Empty;

    [Display(Name = "Широта (Latitude)")]
    [Range(-90.0, 90.0, ErrorMessage = "Широта має бути в діапазоні [-90; 90]")]
    public double Latitude { get; set; }

    [Display(Name = "Довгота (Longitude)")]
    [Range(-180.0, 180.0, ErrorMessage = "Довгота має бути в діапазоні [-180; 180]")]
    public double Longitude { get; set; }

    [Required(ErrorMessage = "Ціна за ніч є обов'язковою")]
    [Range(100, 100000, ErrorMessage = "Ціна має бути від 100 до 100 000 грн")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Ціна за ніч (грн)")]
    public decimal PricePerNight { get; set; }

    [Required(ErrorMessage = "Вкажіть місткість гостей")]
    [Range(1, 30, ErrorMessage = "Кількість гостей від 1 до 30")]
    [Display(Name = "Максимум гостей")]
    public int MaxGuests { get; set; }

    [Display(Name = "Головне зображення (URL / Файл)")]
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [Display(Name = "Файл документації / Буклет правил (URL або завантажений файл)")]
    [StringLength(500)]
    public string? DocumentUrl { get; set; }

    [Display(Name = "Контактний телефон")]
    [Phone(ErrorMessage = "Некоректний формат телефону")]
    [StringLength(30)]
    public string? ContactPhone { get; set; }

    [Display(Name = "Дата створення")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required(ErrorMessage = "Оберіть регіон")]
    [Display(Name = "Регіон")]
    public int RegionId { get; set; }

    public Region? Region { get; set; }

    public ICollection<GlampingActivity> GlampingActivities { get; set; } = new List<GlampingActivity>();

    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
