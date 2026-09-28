using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebLab.Data;
using WebLab.Models;
using WebLab.Services;
using WebLab.ViewModels;

namespace WebLab.Controllers;

public class GlampingSitesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IFileService _fileService;

    public GlampingSitesController(ApplicationDbContext context, IFileService fileService)
    {
        _context = context;
        _fileService = fileService;
    }

    public async Task<IActionResult> Index(int? regionId, string? searchString)
    {
        var query = _context.GlampingSites
            .AsSplitQuery()
            .Include(g => g.Region)
            .Include(g => g.GlampingActivities)
                .ThenInclude(ga => ga.Activity)
            .Include(g => g.Reviews)
            .AsQueryable();

        if (regionId.HasValue && regionId.Value > 0)
        {
            query = query.Where(g => g.RegionId == regionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchString))
        {
            var searchLower = searchString.Trim().ToLower();
            query = query.Where(g => g.Name.ToLower().Contains(searchLower) ||
                                     g.Address.ToLower().Contains(searchLower) ||
                                     g.Description.ToLower().Contains(searchLower));
        }

        ViewBag.Regions = new SelectList(await _context.Regions.OrderBy(r => r.Name).ToListAsync(), "Id", "Name", regionId);
        ViewBag.CurrentRegionId = regionId;
        ViewBag.CurrentSearch = searchString;

        var list = await query.OrderByDescending(g => g.CreatedAt).ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var glampingSite = await _context.GlampingSites
            .AsSplitQuery()
            .Include(g => g.Region)
            .Include(g => g.GlampingActivities)
                .ThenInclude(ga => ga.Activity)
            .Include(g => g.Reviews)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (glampingSite == null) return NotFound();

        return View(glampingSite);
    }

    public async Task<IActionResult> Create()
    {
        var viewModel = new GlampingSiteFormViewModel
        {
            RegionsList = new SelectList(await _context.Regions.OrderBy(r => r.Name).ToListAsync(), "Id", "Name"),
            AvailableActivities = await _context.Activities.OrderBy(a => a.Name).ToListAsync()
        };
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GlampingSiteFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var glampingSite = new GlampingSite
            {
                Name = model.Name,
                Description = model.Description,
                Address = model.Address,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                PricePerNight = model.PricePerNight,
                MaxGuests = model.MaxGuests,
                ContactPhone = model.ContactPhone,
                RegionId = model.RegionId,
                CreatedAt = DateTime.UtcNow
            };

            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                glampingSite.ImageUrl = await _fileService.SaveFileAsync(model.ImageFile, "images");
            }
            else if (!string.IsNullOrWhiteSpace(model.ImageUrl))
            {
                glampingSite.ImageUrl = model.ImageUrl;
            }

            if (model.DocumentFile != null && model.DocumentFile.Length > 0)
            {
                glampingSite.DocumentUrl = await _fileService.SaveFileAsync(model.DocumentFile, "documents");
            }
            else if (!string.IsNullOrWhiteSpace(model.DocumentUrl))
            {
                glampingSite.DocumentUrl = model.DocumentUrl;
            }

            _context.Add(glampingSite);
            await _context.SaveChangesAsync();

            if (model.SelectedActivityIds != null && model.SelectedActivityIds.Any())
            {
                foreach (var actId in model.SelectedActivityIds)
                {
                    _context.GlampingActivities.Add(new GlampingActivity
                    {
                        GlampingSiteId = glampingSite.Id,
                        ActivityId = actId
                    });
                }
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = $"Глемпінг \"{glampingSite.Name}\" успішно створено!";
            return RedirectToAction(nameof(Index));
        }

        model.RegionsList = new SelectList(await _context.Regions.OrderBy(r => r.Name).ToListAsync(), "Id", "Name", model.RegionId);
        model.AvailableActivities = await _context.Activities.OrderBy(a => a.Name).ToListAsync();
        return View(model);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var glampingSite = await _context.GlampingSites
            .Include(g => g.GlampingActivities)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (glampingSite == null) return NotFound();

        var viewModel = new GlampingSiteFormViewModel
        {
            Id = glampingSite.Id,
            Name = glampingSite.Name,
            Description = glampingSite.Description,
            Address = glampingSite.Address,
            Latitude = glampingSite.Latitude,
            Longitude = glampingSite.Longitude,
            PricePerNight = glampingSite.PricePerNight,
            MaxGuests = glampingSite.MaxGuests,
            ContactPhone = glampingSite.ContactPhone,
            RegionId = glampingSite.RegionId,
            ExistingImageUrl = glampingSite.ImageUrl,
            ImageUrl = glampingSite.ImageUrl,
            ExistingDocumentUrl = glampingSite.DocumentUrl,
            DocumentUrl = glampingSite.DocumentUrl,
            SelectedActivityIds = glampingSite.GlampingActivities.Select(ga => ga.ActivityId).ToList(),
            RegionsList = new SelectList(await _context.Regions.OrderBy(r => r.Name).ToListAsync(), "Id", "Name", glampingSite.RegionId),
            AvailableActivities = await _context.Activities.OrderBy(a => a.Name).ToListAsync()
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, GlampingSiteFormViewModel model)
    {
        if (id != model.Id) return NotFound();

        if (ModelState.IsValid)
        {
            var glampingSite = await _context.GlampingSites
                .Include(g => g.GlampingActivities)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (glampingSite == null) return NotFound();

            glampingSite.Name = model.Name;
            glampingSite.Description = model.Description;
            glampingSite.Address = model.Address;
            glampingSite.Latitude = model.Latitude;
            glampingSite.Longitude = model.Longitude;
            glampingSite.PricePerNight = model.PricePerNight;
            glampingSite.MaxGuests = model.MaxGuests;
            glampingSite.ContactPhone = model.ContactPhone;
            glampingSite.RegionId = model.RegionId;

            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                _fileService.DeleteFile(glampingSite.ImageUrl);
                glampingSite.ImageUrl = await _fileService.SaveFileAsync(model.ImageFile, "images");
            }
            else if (!string.IsNullOrWhiteSpace(model.ImageUrl))
            {
                glampingSite.ImageUrl = model.ImageUrl;
            }

            if (model.DocumentFile != null && model.DocumentFile.Length > 0)
            {
                _fileService.DeleteFile(glampingSite.DocumentUrl);
                glampingSite.DocumentUrl = await _fileService.SaveFileAsync(model.DocumentFile, "documents");
            }
            else if (!string.IsNullOrWhiteSpace(model.DocumentUrl))
            {
                glampingSite.DocumentUrl = model.DocumentUrl;
            }

            var currentActivityIds = glampingSite.GlampingActivities.Select(ga => ga.ActivityId).ToHashSet();
            var newActivityIds = (model.SelectedActivityIds ?? new List<int>()).ToHashSet();

            var toRemove = glampingSite.GlampingActivities.Where(ga => !newActivityIds.Contains(ga.ActivityId)).ToList();
            foreach (var item in toRemove)
            {
                _context.GlampingActivities.Remove(item);
            }

            var toAdd = newActivityIds.Where(actId => !currentActivityIds.Contains(actId)).ToList();
            foreach (var actId in toAdd)
            {
                _context.GlampingActivities.Add(new GlampingActivity
                {
                    GlampingSiteId = glampingSite.Id,
                    ActivityId = actId
                });
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Глемпінг \"{glampingSite.Name}\" успішно оновлено!";
            return RedirectToAction(nameof(Index));
        }

        model.RegionsList = new SelectList(await _context.Regions.OrderBy(r => r.Name).ToListAsync(), "Id", "Name", model.RegionId);
        model.AvailableActivities = await _context.Activities.OrderBy(a => a.Name).ToListAsync();
        return View(model);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var glampingSite = await _context.GlampingSites
            .Include(g => g.Region)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (glampingSite == null) return NotFound();

        return View(glampingSite);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var glampingSite = await _context.GlampingSites.FindAsync(id);
        if (glampingSite != null)
        {
            _fileService.DeleteFile(glampingSite.ImageUrl);
            _fileService.DeleteFile(glampingSite.DocumentUrl);
            _context.GlampingSites.Remove(glampingSite);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Глемпінг успішно видалено!";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReview(int glampingSiteId, [Bind("AuthorName,Rating,Comment")] Review review)
    {
        review.GlampingSiteId = glampingSiteId;
        review.CreatedAt = DateTime.UtcNow;

        if (ModelState.IsValid)
        {
            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Дякуємо за ваш відгук!";
        }
        else
        {
            TempData["ErrorMessage"] = "Будь ласка, заповніть обов'язкові поля відгуку (ім'я та коментар).";
        }

        return RedirectToAction(nameof(Details), new { id = glampingSiteId });
    }
}
