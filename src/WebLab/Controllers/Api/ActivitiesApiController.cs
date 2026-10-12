using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebLab.Data;
using WebLab.DTOs;

namespace WebLab.Controllers.Api;

[ApiController]
[Route("api/activities")]
public class ActivitiesApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private const string ActivitiesCacheKey = "api_activities_all";

    public ActivitiesApiController(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ActivityDto>>> GetAll()
    {
        if (_cache.TryGetValue(ActivitiesCacheKey, out List<ActivityDto>? cachedActivities) && cachedActivities != null)
        {
            Response.Headers["X-Cache"] = "HIT";
            return Ok(cachedActivities);
        }

        var activities = await _context.Activities
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new ActivityDto
            {
                Id = a.Id,
                Name = a.Name,
                IconClass = a.IconClass
            })
            .ToListAsync();

        _cache.Set(ActivitiesCacheKey, activities, TimeSpan.FromMinutes(10));
        Response.Headers["X-Cache"] = "MISS";
        return Ok(activities);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ActivityDto>> GetById(int id)
    {
        var cacheKey = $"api_activity_{id}";
        if (_cache.TryGetValue(cacheKey, out ActivityDto? cachedActivity) && cachedActivity != null)
        {
            Response.Headers["X-Cache"] = "HIT";
            return Ok(cachedActivity);
        }

        var activity = await _context.Activities
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
        {
            return NotFound(new { message = $"Активність із ID {id} не знайдено." });
        }

        var dto = new ActivityDto
        {
            Id = activity.Id,
            Name = activity.Name,
            IconClass = activity.IconClass
        };

        _cache.Set(cacheKey, dto, TimeSpan.FromMinutes(10));
        Response.Headers["X-Cache"] = "MISS";
        return Ok(dto);
    }
}
