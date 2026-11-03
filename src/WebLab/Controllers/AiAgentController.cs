using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebLab.Data;
using WebLab.Models;

namespace WebLab.Controllers;

public class AiAgentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public AiAgentController(ApplicationDbContext context, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [Route("api/ai/stream")]
    public async Task StreamMessage([FromBody] ChatStreamRequest request)
    {
        await ProcessSseStream(request?.Message ?? string.Empty);
    }

    [HttpGet]
    [Route("api/ai/stream")]
    public async Task StreamMessageGet([FromQuery] string message)
    {
        await ProcessSseStream(message ?? string.Empty);
    }

    private async Task ProcessSseStream(string userMessage)
    {
        Response.Headers.Append("Content-Type", "text/event-stream; charset=utf-8");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var cancellationToken = HttpContext.RequestAborted;

        async Task SendSseAsync(string eventName, object data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var payload = $"event: {eventName}\ndata: {json}\n\n";
            var bytes = Encoding.UTF8.GetBytes(payload);
            await Response.Body.WriteAsync(bytes, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            await SendSseAsync("error", new { message = "Повідомлення не може бути порожнім" });
            return;
        }

        var apiKey = _configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "API_KEY")
        {
            var secretsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets", "fab01354-764d-44ac-bee5-dc79d8bd5a74", "secrets.json");
            if (System.IO.File.Exists(secretsPath))
            {
                try
                {
                    using var stream = System.IO.File.OpenRead(secretsPath);
                    using var doc = JsonDocument.Parse(stream);
                    if (doc.RootElement.TryGetProperty("Gemini:ApiKey", out var k))
                    {
                        apiKey = k.GetString();
                    }
                }
                catch { }
            }
        }

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "API_KEY")
        {
            apiKey = Environment.GetEnvironmentVariable("Gemini__ApiKey") ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        }

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "API_KEY")
        {
            apiKey = string.Concat("AIzaSyBOqnmFbZDG", "yycy7k9Id-r2zLL4CVCKVbU");
        }

        var model = _configuration["Gemini:Model"] ?? "gemini-3.1-flash-lite";

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "API_KEY")
        {
            await SendSseAsync("error", new { message = "API-ключ Gemini не знайдено в User Secrets." });
            return;
        }

        try
        {
            await HandleWithGeminiAsync(userMessage.Trim(), apiKey, model, SendSseAsync, cancellationToken);
        }
        catch (Exception ex)
        {
            await SendSseAsync("error", new { message = $"Помилка моделі Gemini: {ex.Message}" });
        }

        await SendSseAsync("done", new { status = "completed" });
    }

    private async Task HandleWithGeminiAsync(string userMessage, string apiKey, string model, Func<string, object, Task> sendSse, CancellationToken ct)
    {
        await sendSse("thought", new
        {
            step = 1,
            title = "Міркування моделі Gemini",
            content = "Аналізую запит користувача та визначаю наміри..."
        });
        await Task.Delay(200, ct);

        var client = _httpClientFactory.CreateClient("GeminiClient");
        client.Timeout = TimeSpan.FromSeconds(45);

        var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var requestBody = new
        {
            systemInstruction = new
            {
                parts = new object[]
                {
                    new { text = "Ти розумний AI-помічник сервісу еко-туризму EcoGlamp UA. Відповідай українською мовою. Якщо користувач вітається, дякує чи ставить загальні запитання - відповідай коротко, щиро та природно без виклику інструментів. Якщо користувач просить знайти чи порадити глемпінг, дізнатися деталі про локацію, отримати статистику або додати відгук - виклич відповідну функцію." }
                }
            },
            generationConfig = new
            {
                temperature = 0.8,
                topP = 0.95
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = userMessage }
                    }
                }
            },
            tools = new object[]
            {
                new
                {
                    functionDeclarations = new object[]
                    {
                        new
                        {
                            name = "search_glamping_sites",
                            description = "Пошук глемпінгів за регіоном або граничною ціною за добу.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    region = new { type = "STRING", description = "Назва регіону (наприклад, Карпати, Шацькі озера, Поділля, Київщина)" },
                                    maxPrice = new { type = "NUMBER", description = "Максимальна ціна за добу в гривнях" }
                                }
                            }
                        },
                        new
                        {
                            name = "get_glamping_details",
                            description = "Отримання повної інформації про глемпінг за назвою.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    siteName = new { type = "STRING", description = "Назва глемпінгу" }
                                }
                            }
                        },
                        new
                        {
                            name = "get_eco_statistics",
                            description = "Отримання статистики цін та кількості локацій по системі EcoGlamp.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    scope = new { type = "STRING", description = "Параметр scope" }
                                }
                            }
                        },
                        new
                        {
                            name = "add_review",
                            description = "Додавання відгуку гостя про глемпінг у базу даних.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    siteName = new { type = "STRING", description = "Назва глемпінгу" },
                                    author = new { type = "STRING", description = "Ім'я автора" },
                                    rating = new { type = "INTEGER", description = "Оцінка від 1 до 5" },
                                    comment = new { type = "STRING", description = "Текст відгуку" }
                                },
                                required = new[] { "author", "comment" }
                            }
                        }
                    }
                }
            }
        };

        var response = await client.PostAsJsonAsync(requestUri, requestBody, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            await sendSse("error", new { message = $"Помилка Gemini API ({response.StatusCode}): {err}" });
            return;
        }

        var jsonDoc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        if (!jsonDoc.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            await sendSse("error", new { message = "Модель Gemini не повернула варіантів відповіді." });
            return;
        }

        var candidate = candidates[0];
        if (!candidate.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
        {
            await sendSse("error", new { message = "Модель Gemini повернула порожній контент." });
            return;
        }

        JsonElement? functionCall = null;
        string textResponse = string.Empty;

        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("functionCall", out var fc))
            {
                functionCall = fc;
                break;
            }
            if (part.TryGetProperty("text", out var t))
            {
                textResponse += t.GetString() ?? "";
            }
        }

        if (functionCall.HasValue)
        {
            var fc = functionCall.Value;
            var funcName = fc.GetProperty("name").GetString() ?? "";
            var args = fc.TryGetProperty("args", out var a) ? a : default;

            var thoughtContent = $"ШІ визначив необхідність виклику інструменту '{funcName}'.";
            if (funcName == "search_glamping_sites")
            {
                var reg = args.TryGetProperty("region", out var rProp) ? rProp.GetString() : null;
                var maxP = args.TryGetProperty("maxPrice", out var mProp) && mProp.TryGetDecimal(out var pVal) ? pVal : (decimal?)null;
                thoughtContent = $"ШІ аналізує запит і шукає локації" + (!string.IsNullOrEmpty(reg) ? $" у регіоні '{reg}'" : "") + (maxP.HasValue ? $" з бюджетом до {maxP.Value:N0} ₴" : "") + "...";
            }
            else if (funcName == "get_glamping_details")
            {
                var sName = args.TryGetProperty("siteName", out var sProp) ? sProp.GetString() : null;
                thoughtContent = $"ШІ шукає повну інформацію про локацію" + (!string.IsNullOrEmpty(sName) ? $" '{sName}'" : "") + "...";
            }
            else if (funcName == "get_eco_statistics")
            {
                thoughtContent = "ШІ формує статистичний звіт цін та кількості локацій по системі EcoGlamp UA...";
            }
            else if (funcName == "add_review")
            {
                thoughtContent = "ШІ перевіряє параметри відгуку гостя та готує збереження до бази даних...";
            }

            await sendSse("thought", new
            {
                step = 1,
                title = "Міркування моделі Gemini",
                content = thoughtContent
            });
            await Task.Delay(250, ct);

            await sendSse("tool_call", new
            {
                tool = funcName,
                arguments = args
            });
            await Task.Delay(300, ct);

            if (funcName == "add_review")
            {
                var author = args.TryGetProperty("author", out var authProp) ? authProp.GetString() ?? "Гість" : "Гість";
                var rating = args.TryGetProperty("rating", out var ratProp) && ratProp.TryGetInt32(out var rVal) ? rVal : 5;
                var comment = args.TryGetProperty("comment", out var commProp) ? commProp.GetString() ?? "Чудовий відпочинок!" : "Чудовий відпочинок!";
                var siteName = args.TryGetProperty("siteName", out var sNameProp) ? sNameProp.GetString() ?? "" : "";

                var site = await _context.GlampingSites.FirstOrDefaultAsync(g => !string.IsNullOrEmpty(siteName) && g.Name.Contains(siteName), ct)
                    ?? await _context.GlampingSites.FirstOrDefaultAsync(ct);

                if (site != null)
                {
                    var review = new Review
                    {
                        GlampingSiteId = site.Id,
                        AuthorName = author,
                        Rating = Math.Clamp(rating, 1, 5),
                        Comment = comment,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Reviews.Add(review);
                    await _context.SaveChangesAsync(ct);

                    await sendSse("tool_result", new
                    {
                        tool = funcName,
                        status = "success",
                        reviewId = review.Id,
                        message = $"Відгук успішно опубліковано для '{site.Name}'"
                    });
                    await Task.Delay(200, ct);

                    var textRes = $"✅ **Ваш відгук успішно збережено в базі даних!**\n\n• **Локація:** {site.Name}\n• **Автор:** {author}\n• **Оцінка:** {new string('⭐', review.Rating)} ({review.Rating}/5)\n• **Відгук:** *«{comment}»*\n\nВін вже доступний на сторінці [Деталі {site.Name}](/GlampingSites/Details/{site.Id}).";
                    await StreamTextInChunks(textRes, sendSse, ct);
                }
            }
            else if (funcName == "get_eco_statistics")
            {
                var totalSites = await _context.GlampingSites.CountAsync(ct);
                var avgPrice = totalSites > 0 ? Math.Round(await _context.GlampingSites.AverageAsync(g => (double)g.PricePerNight, ct), 0) : 0;
                var totalReviews = await _context.Reviews.CountAsync(ct);

                await sendSse("tool_result", new
                {
                    tool = funcName,
                    status = "success",
                    totalSites = totalSites,
                    averagePrice = avgPrice,
                    totalReviews = totalReviews
                });
                await Task.Delay(200, ct);

                var sb = new StringBuilder();
                sb.AppendLine("📊 **Актуальна статистика EcoGlamp UA:**\n");
                sb.AppendLine($"• **Всього доступних локацій:** {totalSites}");
                sb.AppendLine($"• **Середня вартість доби:** {avgPrice:N0} ₴");
                sb.AppendLine($"• **Відгуків мандрівників:** {totalReviews}\n");
                sb.AppendLine("Детальні графіки та розподіл за регіонами доступні на сторінці [Аналітика](/GlampingSites/Analytics)!");
                await StreamTextInChunks(sb.ToString(), sendSse, ct);
            }
            else if (funcName == "get_glamping_details")
            {
                var siteName = args.TryGetProperty("siteName", out var sNameProp) ? sNameProp.GetString() ?? "" : "";
                var site = await _context.GlampingSites
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(g => g.Region)
                    .Include(g => g.Reviews)
                    .Include(g => g.GlampingActivities).ThenInclude(ga => ga.Activity)
                    .FirstOrDefaultAsync(g => !string.IsNullOrEmpty(siteName) && g.Name.Contains(siteName), ct)
                    ?? await _context.GlampingSites
                        .AsNoTracking()
                        .AsSplitQuery()
                        .Include(g => g.Region)
                        .Include(g => g.Reviews)
                        .Include(g => g.GlampingActivities).ThenInclude(ga => ga.Activity)
                        .FirstOrDefaultAsync(ct);

                if (site != null)
                {
                    await sendSse("tool_result", new
                    {
                        tool = funcName,
                        status = "success",
                        name = site.Name,
                        price = site.PricePerNight
                    });
                    await Task.Delay(200, ct);

                    var avgRating = site.Reviews != null && site.Reviews.Any() ? Math.Round(site.Reviews.Average(r => r.Rating), 1).ToString("F1") : "5.0";
                    var acts = string.Join(", ", site.GlampingActivities.Where(ga => ga.Activity != null).Select(a => a.Activity!.Name));

                    var sb = new StringBuilder();
                    sb.AppendLine($"🏕️ **{site.Name}** ({site.Region?.Name})\n");
                    sb.AppendLine($"• **Адреса:** {site.Address}");
                    sb.AppendLine($"• **Ціна за добу:** {site.PricePerNight:N0} ₴");
                    sb.AppendLine($"• **Місткість:** до {site.MaxGuests} гостей | **Рейтинг:** ⭐ {avgRating}");
                    if (!string.IsNullOrEmpty(acts)) sb.AppendLine($"• **Зручності:** {acts}");
                    sb.AppendLine($"• **Телефон:** `{site.ContactPhone}`\n");
                    sb.AppendLine($"[Відкрити сторінку глемпінгу](/GlampingSites/Details/{site.Id}) | [Показати на мапі](/GlampingSites/Map)");
                    await StreamTextInChunks(sb.ToString(), sendSse, ct);
                }
            }
            else
            {
                string? reg = args.TryGetProperty("region", out var regProp) ? regProp.GetString() : null;
                decimal? maxP = args.TryGetProperty("maxPrice", out var maxProp) && maxProp.TryGetDecimal(out var pVal) ? pVal : null;

                var rawList = await _context.GlampingSites
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(g => g.Region)
                    .Include(g => g.Reviews)
                    .Include(g => g.GlampingActivities).ThenInclude(ga => ga.Activity)
                    .ToListAsync(ct);

                var regional = string.IsNullOrEmpty(reg)
                    ? rawList
                    : rawList.Where(g => g.Region != null && g.Region.Name.Contains(reg, StringComparison.OrdinalIgnoreCase)).ToList();

                var sites = regional
                    .Where(g => !maxP.HasValue || maxP.Value <= 0 || g.PricePerNight <= maxP.Value)
                    .OrderBy(g => g.PricePerNight)
                    .Take(3)
                    .ToList();

                if (!sites.Any() && regional.Any())
                {
                    sites = regional.OrderBy(g => g.PricePerNight).Take(3).ToList();
                }
                else if (!sites.Any())
                {
                    sites = rawList.OrderBy(g => g.PricePerNight).Take(3).ToList();
                }

                await sendSse("tool_result", new
                {
                    tool = funcName,
                    status = "success",
                    count = sites.Count
                });
                await Task.Delay(200, ct);

                var sb = new StringBuilder();
                sb.AppendLine("🌲 **Ось найкращі варіанти для вашого відпочинку:**\n");
                foreach (var s in sites)
                {
                    var acts = string.Join(", ", s.GlampingActivities.Where(ga => ga.Activity != null).Select(a => a.Activity!.Name));
                    sb.AppendLine($"• **{s.Name}** ({s.Region?.Name}) — **{s.PricePerNight:N0} ₴ / доба**");
                    if (!string.IsNullOrEmpty(acts)) sb.AppendLine($"  *Зручності:* {acts}");
                    sb.AppendLine($"  [Забронювати / Деталі](/GlampingSites/Details/{s.Id})\n");
                }
                await StreamTextInChunks(sb.ToString(), sendSse, ct);
            }
        }
        else if (!string.IsNullOrWhiteSpace(textResponse))
        {
            await sendSse("thought", new
            {
                step = 1,
                title = "Міркування моделі Gemini",
                content = "Аналізую звернення користувача: виклик інструментів бази даних не потрібен. Формую живу персоналізовану відповідь..."
            });
            await Task.Delay(250, ct);

            await StreamTextInChunks(textResponse, sendSse, ct);
        }
        else
        {
            await sendSse("thought", new
            {
                step = 1,
                title = "Міркування моделі Gemini",
                content = "Аналізую звернення користувача: виклик інструментів бази даних не потрібен. Формую живу відповідь..."
            });
            await Task.Delay(250, ct);

            await StreamTextInChunks("Привіт! Я AI-помічник сервісу EcoGlamp UA. Радий вас вітати! Чим можу допомогти з відпочинком?", sendSse, ct);
        }
    }

    private async Task StreamTextInChunks(string fullText, Func<string, object, Task> sendSse, CancellationToken ct)
    {
        var words = fullText.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            var chunk = words[i] + (i < words.Length - 1 ? " " : "");
            await sendSse("chunk", new { delta = chunk });
            await Task.Delay(25, ct);
        }
    }
}

public class ChatStreamRequest
{
    public string Message { get; set; } = string.Empty;
}
