using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebLab.Data;
using WebLab.Models;

namespace WebLab.Controllers;

public class ActivitiesController : Controller
{
    private readonly ApplicationDbContext _context;

    public ActivitiesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var activities = await _context.Activities
            .Include(a => a.GlampingActivities)
            .OrderBy(a => a.Name)
            .ToListAsync();
        return View(activities);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,IconClass")] Activity activity)
    {
        if (ModelState.IsValid)
        {
            _context.Add(activity);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Активність \"{activity.Name}\" додано!";
            return RedirectToAction(nameof(Index));
        }
        return View(activity);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var activity = await _context.Activities.FindAsync(id);
        if (activity == null) return NotFound();
        return View(activity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,IconClass")] Activity activity)
    {
        if (id != activity.Id) return NotFound();

        if (ModelState.IsValid)
        {
            _context.Update(activity);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Активність \"{activity.Name}\" оновлено!";
            return RedirectToAction(nameof(Index));
        }
        return View(activity);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var activity = await _context.Activities
            .Include(a => a.GlampingActivities)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (activity == null) return NotFound();

        return View(activity);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var activity = await _context.Activities.FindAsync(id);
        if (activity != null)
        {
            _context.Activities.Remove(activity);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Активність успішно видалено!";
        }
        return RedirectToAction(nameof(Index));
    }
}
