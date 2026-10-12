using System.ComponentModel.DataAnnotations;

namespace WebLab.DTOs;

public class CreateGlampingSiteDto
{
    [Required(ErrorMessage = "Назва глемпінгу є обов'язковою")]
    [StringLength(150, ErrorMessage = "Назва не може перевищувати 150 символів")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Опис є обов'язковим")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Адреса є обов'язковою")]
    [StringLength(200)]
    public string Address { get; set; } = string.Empty;

    [Range(-90.0, 90.0, ErrorMessage = "Широта має бути в діапазоні [-90; 90]")]
    public double Latitude { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "Довгота має бути в діапазоні [-180; 180]")]
    public double Longitude { get; set; }

    [Required(ErrorMessage = "Ціна за ніч є обов'язковою")]
    [Range(100, 100000, ErrorMessage = "Ціна має бути від 100 до 100 000 грн")]
    public decimal PricePerNight { get; set; }

    [Required(ErrorMessage = "Вкажіть місткість гостей")]
    [Range(1, 30, ErrorMessage = "Кількість гостей від 1 до 30")]
    public int MaxGuests { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [StringLength(500)]
    public string? DocumentUrl { get; set; }

    [Phone(ErrorMessage = "Некоректний формат телефону")]
    [StringLength(30)]
    public string? ContactPhone { get; set; }

    [Required(ErrorMessage = "Оберіть регіон")]
    public int RegionId { get; set; }

    public List<int>? ActivityIds { get; set; }
}
