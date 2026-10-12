using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebLab.Data;
using WebLab.DTOs;
using WebLab.Models;

namespace WebLab.Controllers.Api;

[ApiController]
[Route("api/regions")]
public class RegionsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private const string RegionsCacheKey = "api_regions_all";

    public RegionsApiController(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? term, [FromQuery] string? q)
    {
        var searchQuery = term ?? q;
        if (string.IsNullOrWhiteSpace(searchQuery) || searchQuery.Trim().Length < 3)
        {
            return Ok(new { results = Array.Empty<object>() });
        }

        var lowerQuery = searchQuery.Trim().ToLower();
        var regions = await _context.Regions
            .Where(r => r.Name.ToLower().Contains(lowerQuery))
            .OrderBy(r => r.Name)
            .Take(30)
            .Select(r => new
            {
                id = r.Id,
                text = r.Name
            })
            .ToListAsync();

        return Ok(new { results = regions });
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RegionDto>>> GetAll()
    {
        if (_cache.TryGetValue(RegionsCacheKey, out List<RegionDto>? cachedRegions) && cachedRegions != null)
        {
            Response.Headers["X-Cache"] = "HIT";
            return Ok(cachedRegions);
        }

        var regions = await _context.Regions
            .Include(r => r.GlampingSites)
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RegionDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                SitesCount = r.GlampingSites.Count
            })
            .ToListAsync();

        _cache.Set(RegionsCacheKey, regions, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(2),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        });

        Response.Headers["X-Cache"] = "MISS";
        return Ok(regions);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RegionDto>> GetById(int id)
    {
        var cacheKey = $"api_region_{id}";
        if (_cache.TryGetValue(cacheKey, out RegionDto? cachedRegion) && cachedRegion != null)
        {
            Response.Headers["X-Cache"] = "HIT";
            return Ok(cachedRegion);
        }

        var region = await _context.Regions
            .Include(r => r.GlampingSites)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (region == null)
        {
            return NotFound(new { message = $"Регіон із ID {id} не знайдено." });
        }

        var dto = new RegionDto
        {
            Id = region.Id,
            Name = region.Name,
            Description = region.Description,
            SitesCount = region.GlampingSites.Count
        };

        _cache.Set(cacheKey, dto, TimeSpan.FromMinutes(5));
        Response.Headers["X-Cache"] = "MISS";
        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<RegionDto>> Create([FromBody] CreateRegionDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var region = new Region
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim()
        };

        _context.Regions.Add(region);
        await _context.SaveChangesAsync();

        _cache.Remove(RegionsCacheKey);

        var result = new RegionDto
        {
            Id = region.Id,
            Name = region.Name,
            Description = region.Description,
            SitesCount = 0
        };

        return CreatedAtAction(nameof(GetById), new { id = region.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RegionDto>> Update(int id, [FromBody] CreateRegionDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var region = await _context.Regions
            .Include(r => r.GlampingSites)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (region == null)
        {
            return NotFound(new { message = $"Регіон із ID {id} не знайдено." });
        }

        region.Name = dto.Name.Trim();
        region.Description = dto.Description?.Trim();

        await _context.SaveChangesAsync();

        _cache.Remove(RegionsCacheKey);
        _cache.Remove($"api_region_{id}");

        return Ok(new RegionDto
        {
            Id = region.Id,
            Name = region.Name,
            Description = region.Description,
            SitesCount = region.GlampingSites.Count
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var region = await _context.Regions
            .Include(r => r.GlampingSites)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (region == null)
        {
            return NotFound(new { message = $"Регіон із ID {id} не знайдено." });
        }

        if (region.GlampingSites.Count > 0)
        {
            return BadRequest(new { message = "Неможливо видалити регіон, в якому зареєстровані глемпінги." });
        }

        _context.Regions.Remove(region);
        await _context.SaveChangesAsync();

        _cache.Remove(RegionsCacheKey);
        _cache.Remove($"api_region_{id}");

        return Ok(new { message = $"Регіон '{region.Name}' успішно видалено." });
    }
}
