using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebLab.Data;
using WebLab.DTOs;
using WebLab.Models;
using WebLab.Services;

namespace WebLab.Controllers.Api;

[ApiController]
[Route("api/glampingsites")]
public class GlampingSitesApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IFileService _fileService;
    private readonly IMemoryCache _cache;

    public GlampingSitesApiController(ApplicationDbContext context, IFileService fileService, IMemoryCache cache)
    {
        _context = context;
        _fileService = fileService;
        _cache = cache;
    }

    [HttpGet("autocomplete")]
    public async Task<IActionResult> Autocomplete([FromQuery] string? term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 3)
        {
            return Ok(Array.Empty<object>());
        }

        var lowerTerm = term.Trim().ToLower();
        var results = await _context.GlampingSites
            .Include(g => g.Region)
            .Where(g => g.Name.ToLower().Contains(lowerTerm) || g.Address.ToLower().Contains(lowerTerm))
            .OrderBy(g => g.Name)
            .Take(10)
            .Select(g => new
            {
                id = g.Id,
                label = g.Name + (g.Region != null ? " (" + g.Region.Name + ")" : ""),
                value = g.Name,
                url = "/GlampingSites/Details/" + g.Id,
                price = g.PricePerNight,
                region = g.Region != null ? g.Region.Name : string.Empty,
                image = g.ImageUrl
            })
            .ToListAsync();

        return Ok(results);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<GlampingSiteDto>>> GetAll(
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 10,
        [FromQuery] int? regionId = null,
        [FromQuery] string? search = null)
    {
        if (skip < 0) skip = 0;
        if (limit <= 0) limit = 10;
        if (limit > 100) limit = 100;

        var cacheKey = $"api_glampings_{skip}_{limit}_{regionId}_{search}";
        if (_cache.TryGetValue(cacheKey, out PagedResponse<GlampingSiteDto>? cachedResponse) && cachedResponse != null)
        {
            Response.Headers["X-Cache"] = "HIT";
            return Ok(cachedResponse);
        }

        var query = _context.GlampingSites
            .Include(g => g.Region)
            .Include(g => g.GlampingActivities)
                .ThenInclude(ga => ga.Activity)
            .Include(g => g.Reviews)
            .AsNoTracking()
            .AsQueryable();

        if (regionId.HasValue)
        {
            query = query.Where(g => g.RegionId == regionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(g => g.Name.ToLower().Contains(searchLower) ||
                                     g.Address.ToLower().Contains(searchLower));
        }

        var totalCount = await query.CountAsync();

        var sites = await query
            .OrderByDescending(g => g.CreatedAt)
            .Skip(skip)
            .Take(limit)
            .ToListAsync();

        var items = sites.Select(MapToDto).ToList();

        string? nextLink = null;
        if (skip + limit < totalCount)
        {
            var queryParams = new List<string>
            {
                $"skip={skip + limit}",
                $"limit={limit}"
            };

            if (regionId.HasValue)
            {
                queryParams.Add($"regionId={regionId.Value}");
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                queryParams.Add($"search={Uri.EscapeDataString(search)}");
            }

            nextLink = $"{Request.Path}?{string.Join("&", queryParams)}";
        }

        var response = new PagedResponse<GlampingSiteDto>
        {
            Items = items,
            TotalCount = totalCount,
            Skip = skip,
            Limit = limit,
            NextLink = nextLink
        };

        _cache.Set(cacheKey, response, TimeSpan.FromSeconds(60));
        Response.Headers["X-Cache"] = "MISS";
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GlampingSiteDto>> GetById(int id)
    {
        var cacheKey = $"api_glamping_{id}";
        if (_cache.TryGetValue(cacheKey, out GlampingSiteDto? cachedSite) && cachedSite != null)
        {
            Response.Headers["X-Cache"] = "HIT";
            return Ok(cachedSite);
        }

        var site = await _context.GlampingSites
            .Include(g => g.Region)
            .Include(g => g.GlampingActivities)
                .ThenInclude(ga => ga.Activity)
            .Include(g => g.Reviews)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id);

        if (site == null)
        {
            return NotFound(new { message = $"Глемпінг із ID {id} не знайдено." });
        }

        var dto = MapToDto(site);
        _cache.Set(cacheKey, dto, TimeSpan.FromMinutes(2));
        Response.Headers["X-Cache"] = "MISS";
        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<GlampingSiteDto>> Create([FromBody] CreateGlampingSiteDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var regionExists = await _context.Regions.AnyAsync(r => r.Id == dto.RegionId);
        if (!regionExists)
        {
            return BadRequest(new { message = "Вказаний регіон не існує." });
        }

        var site = new GlampingSite
        {
            Name = dto.Name.Trim(),
            Description = dto.Description.Trim(),
            Address = dto.Address.Trim(),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            PricePerNight = dto.PricePerNight,
            MaxGuests = dto.MaxGuests,
            ImageUrl = dto.ImageUrl,
            DocumentUrl = dto.DocumentUrl,
            ContactPhone = dto.ContactPhone,
            RegionId = dto.RegionId,
            CreatedAt = DateTime.UtcNow
        };

        if (dto.ActivityIds != null && dto.ActivityIds.Count > 0)
        {
            var validActivityIds = await _context.Activities
                .Where(a => dto.ActivityIds.Contains(a.Id))
                .Select(a => a.Id)
                .ToListAsync();

            foreach (var actId in validActivityIds)
            {
                site.GlampingActivities.Add(new GlampingActivity
                {
                    ActivityId = actId,
                    GlampingSite = site
                });
            }
        }

        _context.GlampingSites.Add(site);
        await _context.SaveChangesAsync();

        var created = await _context.GlampingSites
            .Include(g => g.Region)
            .Include(g => g.GlampingActivities)
                .ThenInclude(ga => ga.Activity)
            .Include(g => g.Reviews)
            .FirstAsync(g => g.Id == site.Id);

        return CreatedAtAction(nameof(GetById), new { id = site.Id }, MapToDto(created));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<GlampingSiteDto>> Update(int id, [FromBody] UpdateGlampingSiteDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var site = await _context.GlampingSites
            .Include(g => g.GlampingActivities)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (site == null)
        {
            return NotFound(new { message = $"Глемпінг із ID {id} не знайдено." });
        }

        var regionExists = await _context.Regions.AnyAsync(r => r.Id == dto.RegionId);
        if (!regionExists)
        {
            return BadRequest(new { message = "Вказаний регіон не існує." });
        }

        site.Name = dto.Name.Trim();
        site.Description = dto.Description.Trim();
        site.Address = dto.Address.Trim();
        site.Latitude = dto.Latitude;
        site.Longitude = dto.Longitude;
        site.PricePerNight = dto.PricePerNight;
        site.MaxGuests = dto.MaxGuests;
        site.ImageUrl = dto.ImageUrl;
        site.DocumentUrl = dto.DocumentUrl;
        site.ContactPhone = dto.ContactPhone;
        site.RegionId = dto.RegionId;

        site.GlampingActivities.Clear();
        if (dto.ActivityIds != null && dto.ActivityIds.Count > 0)
        {
            var validActivityIds = await _context.Activities
                .Where(a => dto.ActivityIds.Contains(a.Id))
                .Select(a => a.Id)
                .ToListAsync();

            foreach (var actId in validActivityIds)
            {
                site.GlampingActivities.Add(new GlampingActivity
                {
                    GlampingSiteId = site.Id,
                    ActivityId = actId
                });
            }
        }

        await _context.SaveChangesAsync();

        var updated = await _context.GlampingSites
            .Include(g => g.Region)
            .Include(g => g.GlampingActivities)
                .ThenInclude(ga => ga.Activity)
            .Include(g => g.Reviews)
            .FirstAsync(g => g.Id == site.Id);

        return Ok(MapToDto(updated));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var site = await _context.GlampingSites.FirstOrDefaultAsync(g => g.Id == id);
        if (site == null)
        {
            return NotFound(new { message = $"Глемпінг із ID {id} не знайдено." });
        }

        _fileService.DeleteFile(site.ImageUrl);
        _fileService.DeleteFile(site.DocumentUrl);

        _context.GlampingSites.Remove(site);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Глемпінг '{site.Name}' успішно видалено." });
    }

    private static GlampingSiteDto MapToDto(GlampingSite site)
    {
        var reviewsCount = site.Reviews?.Count ?? 0;
        var avgRating = reviewsCount > 0 ? site.Reviews!.Average(r => r.Rating) : 0.0;

        return new GlampingSiteDto
        {
            Id = site.Id,
            Name = site.Name,
            Description = site.Description,
            Address = site.Address,
            Latitude = site.Latitude,
            Longitude = site.Longitude,
            PricePerNight = site.PricePerNight,
            MaxGuests = site.MaxGuests,
            ImageUrl = site.ImageUrl,
            DocumentUrl = site.DocumentUrl,
            ContactPhone = site.ContactPhone,
            CreatedAt = site.CreatedAt,
            RegionId = site.RegionId,
            RegionName = site.Region?.Name,
            ActivityIds = site.GlampingActivities.Select(ga => ga.ActivityId).ToList(),
            ActivityNames = site.GlampingActivities
                .Where(ga => ga.Activity != null)
                .Select(ga => ga.Activity!.Name)
                .ToList(),
            AverageRating = Math.Round(avgRating, 1),
            ReviewsCount = reviewsCount
        };
    }
}
