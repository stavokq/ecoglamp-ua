using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebLab.Data;
using WebLab.Models;

namespace WebLab.Controllers;

public class RegionsController : Controller
{
    private readonly ApplicationDbContext _context;

    public RegionsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var regions = await _context.Regions
            .Include(r => r.GlampingSites)
            .OrderBy(r => r.Name)
            .ToListAsync();
        return View(regions);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Description")] Region region)
    {
        if (ModelState.IsValid)
        {
            _context.Add(region);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Регіон \"{region.Name}\" успішно створено!";
            return RedirectToAction(nameof(Index));
        }
        return View(region);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var region = await _context.Regions.FindAsync(id);
        if (region == null) return NotFound();
        return View(region);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description")] Region region)
    {
        if (id != region.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(region);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Регіон \"{region.Name}\" успішно оновлено!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Regions.Any(e => e.Id == region.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(region);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var region = await _context.Regions
            .Include(r => r.GlampingSites)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (region == null) return NotFound();

        return View(region);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var region = await _context.Regions
            .Include(r => r.GlampingSites)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (region != null)
        {
            if (region.GlampingSites.Any())
            {
                TempData["ErrorMessage"] = "Неможливо видалити регіон, у якому є зареєстровані глемпінги!";
                return RedirectToAction(nameof(Index));
            }

            _context.Regions.Remove(region);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Регіон успішно видалено!";
        }

        return RedirectToAction(nameof(Index));
    }
}
