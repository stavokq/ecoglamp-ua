using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebLab.Data;

namespace WebLab.Controllers;

public class SeoController : Controller
{
    private readonly ApplicationDbContext _context;

    public SeoController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Route("sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var sites = await _context.GlampingSites.AsNoTracking().ToListAsync();
        var regions = await _context.Regions.AsNoTracking().ToListAsync();

        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        xml.AppendLine("  <url>");
        xml.AppendLine($"    <loc>{baseUrl}/</loc>");
        xml.AppendLine("    <changefreq>daily</changefreq>");
        xml.AppendLine("    <priority>1.0</priority>");
        xml.AppendLine("  </url>");

        xml.AppendLine("  <url>");
        xml.AppendLine($"    <loc>{baseUrl}/GlampingSites/Map</loc>");
        xml.AppendLine("    <changefreq>weekly</changefreq>");
        xml.AppendLine("    <priority>0.9</priority>");
        xml.AppendLine("  </url>");

        xml.AppendLine("  <url>");
        xml.AppendLine($"    <loc>{baseUrl}/GlampingSites/Analytics</loc>");
        xml.AppendLine("    <changefreq>weekly</changefreq>");
        xml.AppendLine("    <priority>0.8</priority>");
        xml.AppendLine("  </url>");

        foreach (var site in sites)
        {
            var lastMod = site.CreatedAt.ToString("yyyy-MM-dd");
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{baseUrl}/GlampingSites/Details/{site.Id}</loc>");
            xml.AppendLine($"    <lastmod>{lastMod}</lastmod>");
            xml.AppendLine("    <changefreq>weekly</changefreq>");
            xml.AppendLine("    <priority>0.8</priority>");
            xml.AppendLine("  </url>");
        }

        foreach (var region in regions)
        {
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{baseUrl}/GlampingSites?regionId={region.Id}</loc>");
            xml.AppendLine("    <changefreq>weekly</changefreq>");
            xml.AppendLine("    <priority>0.7</priority>");
            xml.AppendLine("  </url>");
        }

        xml.AppendLine("</urlset>");

        return Content(xml.ToString(), "application/xml", Encoding.UTF8);
    }
}
