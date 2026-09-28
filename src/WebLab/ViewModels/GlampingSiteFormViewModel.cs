using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using WebLab.Models;

namespace WebLab.ViewModels;

public class GlampingSiteFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Назва глемпінгу є обов'язковою")]
    [StringLength(150, ErrorMessage = "Назва не може перевищувати 150 символів")]
    [Display(Name = "Назва глемпінгу")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Опис є обов'язковим")]
    [Display(Name = "Детальний опис")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть адресу або локацію")]
    [StringLength(200)]
    [Display(Name = "Адреса")]
    public string Address { get; set; } = string.Empty;

    [Display(Name = "Широта (Latitude)")]
    [Range(-90.0, 90.0, ErrorMessage = "Широта має бути в діапазоні [-90; 90]")]
    public double Latitude { get; set; }

    [Display(Name = "Довгота (Longitude)")]
    [Range(-180.0, 180.0, ErrorMessage = "Довгота має бути в діапазоні [-180; 180]")]
    public double Longitude { get; set; }

    [Required(ErrorMessage = "Вкажіть ціну за ніч")]
    [Range(100, 100000, ErrorMessage = "Ціна має бути від 100 до 100 000 грн")]
    [Display(Name = "Ціна за добу (грн)")]
    public decimal PricePerNight { get; set; }

    [Required(ErrorMessage = "Вкажіть максимальну кількість гостей")]
    [Range(1, 30, ErrorMessage = "Кількість гостей від 1 до 30")]
    [Display(Name = "Максимум гостей")]
    public int MaxGuests { get; set; }

    [Display(Name = "Контактний телефон")]
    [Phone(ErrorMessage = "Некоректний формат номеру")]
    public string? ContactPhone { get; set; }

    [Display(Name = "Поточне зображення")]
    public string? ExistingImageUrl { get; set; }

    [Display(Name = "Завантажити нове фото (файл)")]
    public IFormFile? ImageFile { get; set; }

    [Display(Name = "Або пряме посилання на фото (URL)")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Поточний документ/правила")]
    public string? ExistingDocumentUrl { get; set; }

    [Display(Name = "Завантажити файл документа / брошуру (PDF, DOCX)")]
    public IFormFile? DocumentFile { get; set; }

    [Display(Name = "Або посилання на файл документа (URL)")]
    public string? DocumentUrl { get; set; }

    [Required(ErrorMessage = "Оберіть регіон")]
    [Display(Name = "Регіон")]
    public int RegionId { get; set; }

    public SelectList? RegionsList { get; set; }

    [Display(Name = "Активності та зручності")]
    public List<int> SelectedActivityIds { get; set; } = new();

    public List<Activity> AvailableActivities { get; set; } = new();
}
