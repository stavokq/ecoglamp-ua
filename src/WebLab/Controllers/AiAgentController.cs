using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        var model = _configuration["Gemini:Model"] ?? "gemini-3.1-flash-lite";

        var handled = false;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                handled = await TryHandleWithGeminiAsync(userMessage.Trim(), apiKey, model, SendSseAsync, cancellationToken);
            }
            catch
            {
                handled = false;
            }
        }

        if (!handled)
        {
            await HandleWithInternalEngineAsync(userMessage.Trim(), SendSseAsync, cancellationToken);
        }

        await SendSseAsync("done", new { status = "completed" });
    }

    private async Task<bool> TryHandleWithGeminiAsync(string userMessage, string apiKey, string model, Func<string, object, Task> sendSse, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(12);

        var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var requestBody = new
        {
            systemInstruction = new
            {
                parts = new object[]
                {
                    new { text = "Ти дружній AI-помічник сервісу еко-туризму EcoGlamp UA. Відповідай українською мовою. Якщо користувач вітається, дякує чи ставить загальні запитання - відповідай коротко, природно та ввічливо без виклику інструментів. Якщо користувач просить підібрати або знайти глемпінг, дізнатися деталі, переглянути статистику чи залишити відгук - виклич відповідну функцію." }
                }
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
                                    region = new { type = "STRING", description = "Назва регіону (наприклад, Карпати, Шацькі озера, Поділля)" },
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
            return false;
        }

        var jsonDoc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        if (!jsonDoc.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return false;
        }

        var candidate = candidates[0];
        if (!candidate.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
        {
            return false;
        }

        var firstPart = parts[0];
        if (firstPart.TryGetProperty("functionCall", out var functionCall))
        {
            var funcName = functionCall.GetProperty("name").GetString() ?? "";
            var args = functionCall.TryGetProperty("args", out var a) ? a : default;

            await sendSse("thought", new
            {
                step = 1,
                title = "Аналіз намірів",
                content = $"Визначаю параметри та викликаю інструмент '{funcName}'..."
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
                    return true;
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
                return true;
            }
            else if (funcName == "get_glamping_details")
            {
                var siteName = args.TryGetProperty("siteName", out var sNameProp) ? sNameProp.GetString() ?? "" : "";
                var site = await _context.GlampingSites
                    .Include(g => g.Region)
                    .Include(g => g.Reviews)
                    .Include(g => g.GlampingActivities).ThenInclude(ga => ga.Activity)
                    .FirstOrDefaultAsync(g => !string.IsNullOrEmpty(siteName) && g.Name.Contains(siteName), ct)
                    ?? await _context.GlampingSites
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
                    return true;
                }
            }
            else
            {
                string? reg = args.TryGetProperty("region", out var regProp) ? regProp.GetString() : null;
                decimal? maxP = args.TryGetProperty("maxPrice", out var maxProp) && maxProp.TryGetDecimal(out var pVal) ? pVal : null;

                var q = _context.GlampingSites
                    .Include(g => g.Region)
                    .Include(g => g.Reviews)
                    .Include(g => g.GlampingActivities).ThenInclude(ga => ga.Activity)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(reg))
                {
                    q = q.Where(g => g.Region != null && g.Region.Name.Contains(reg));
                }
                if (maxP.HasValue && maxP.Value > 0)
                {
                    q = q.Where(g => g.PricePerNight <= maxP.Value);
                }

                var sites = await q.Take(3).ToListAsync(ct);
                if (!sites.Any())
                {
                    sites = await _context.GlampingSites
                        .Include(g => g.Region)
                        .Include(g => g.Reviews)
                        .Include(g => g.GlampingActivities).ThenInclude(ga => ga.Activity)
                        .Take(3)
                        .ToListAsync(ct);
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
                return true;
            }
        }
        else if (firstPart.TryGetProperty("text", out var textProp))
        {
            var text = textProp.GetString() ?? "";
            await StreamTextInChunks(text, sendSse, ct);
            return true;
        }

        return false;
    }

    private async Task HandleWithInternalEngineAsync(string userMessage, Func<string, object, Task> sendSse, CancellationToken ct)
    {
        var lower = userMessage.ToLowerInvariant();

        if (lower.Contains("привіт") || lower.Contains("добрий день") || lower.Contains("вітаю") || lower.Contains("хай") || lower == "hello" || lower == "hi")
        {
            await StreamTextInChunks("Привіт! Я AI-помічник сервісу EcoGlamp UA. Допоможу підібрати затишний купол або намет у Карпатах, на Шацьких озерах чи біля Києва, зорієнтую за цінами або допоможу зберегти ваш відгук. Чим можу допомогти?", sendSse, ct);
            return;
        }

        if (lower.Contains("дякую") || lower.Contains("спасибі"))
        {
            await StreamTextInChunks("Будь ласка! Завжди радий допомогти. Бажаю вам чудового та затишного відпочинку на природі!", sendSse, ct);
            return;
        }

        if (lower.Contains("хто ти") || lower.Contains("що ти вмієш"))
        {
            await StreamTextInChunks("Я ваш гід по еко-туризму в Україні. Я вмію підбирати глемпінги за регіоном та бюджетом, показувати зручності, надавати системну статистику цін та публікувати ваші відгуки.", sendSse, ct);
            return;
        }

        if (lower.Contains("відгук") || lower.Contains("оцін") || lower.Contains("коментар") || lower.Contains("постав"))
        {
            await HandleAddReviewFlow(userMessage, lower, sendSse, ct);
        }
        else if (lower.Contains("статистик") || lower.Contains("скільки") || lower.Contains("середн") || lower.Contains("звіт"))
        {
            await HandleStatisticsFlow(sendSse, ct);
        }
        else if (lower.Contains("детал") || lower.Contains("інфо") || lower.Contains("телефон") || lower.Contains("контакт") || lower.Contains("адрес"))
        {
            await HandleDetailsFlow(userMessage, lower, sendSse, ct);
        }
        else if (lower.Contains("знайди") || lower.Contains("порадь") || lower.Contains("глемпінг") || lower.Contains("карпат") || lower.Contains("шацьк") || lower.Contains("цін") || lower.Contains("бюджет"))
        {
            await HandleSearchFlow(userMessage, lower, sendSse, ct);
        }
        else
        {
            await StreamTextInChunks("Я можу допомогти підібрати глемпінг за регіоном чи бюджетом, показати актуальні ціни або зберегти ваш відгук про відпочинок. Спробуйте запитати: *'Порадь глемпінг у Карпатах до 4000 грн'* або скористайтеся підказками нижче.", sendSse, ct);
        }
    }

    private async Task HandleSearchFlow(string original, string lower, Func<string, object, Task> sendSse, CancellationToken ct)
    {
        await sendSse("thought", new
        {
            step = 1,
            title = "Аналіз намірів",
            content = "Визначаю критерії пошуку: регіон, бюджет, наявність зручностей..."
        });
        await Task.Delay(250, ct);

        string? regionFilter = null;
        if (lower.Contains("карпат")) regionFilter = "Карпати";
        else if (lower.Contains("шацьк")) regionFilter = "Шацькі";
        else if (lower.Contains("київ")) regionFilter = "Київ";
        else if (lower.Contains("поділ") || lower.Contains("товтр") || lower.Contains("бакот")) regionFilter = "Поділ";
        else if (lower.Contains("полісс")) regionFilter = "Полісся";

        decimal? maxPrice = null;
        var matchPrice = Regex.Match(lower, @"(\d{3,5})\s*(грн|₴)?");
        if (matchPrice.Success && decimal.TryParse(matchPrice.Groups[1].Value, out var parsedPrice))
        {
            maxPrice = parsedPrice;
        }

        await sendSse("tool_call", new
        {
            tool = "search_glamping_sites",
            arguments = new
            {
                query = original,
                region = regionFilter,
                maxPrice = maxPrice
            }
        });
        await Task.Delay(300, ct);

        var query = _context.GlampingSites
            .AsNoTracking()
            .Include(g => g.Region)
            .Include(g => g.Reviews)
            .Include(g => g.GlampingActivities).ThenInclude(ga => ga.Activity)
            .AsQueryable();

        if (!string.IsNullOrEmpty(regionFilter))
        {
            query = query.Where(g => g.Region != null && g.Region.Name.Contains(regionFilter));
        }

        if (maxPrice.HasValue && maxPrice.Value > 0)
        {
            query = query.Where(g => g.PricePerNight <= maxPrice.Value);
        }

        var results = await query.Take(4).ToListAsync(ct);

        if (!results.Any())
        {
            results = await _context.GlampingSites
                .AsNoTracking()
                .Include(g => g.Region)
                .Include(g => g.Reviews)
                .Include(g => g.GlampingActivities).ThenInclude(ga => ga.Activity)
                .OrderBy(g => g.PricePerNight)
                .Take(3)
                .ToListAsync(ct);
        }

        await sendSse("tool_result", new
        {
            tool = "search_glamping_sites",
            status = "success",
            count = results.Count
        });
        await Task.Delay(200, ct);

        var responseBuilder = new StringBuilder();
        responseBuilder.AppendLine($"Я підібрав для вас чудові варіанти відпочинку в **EcoGlamp UA**:\n");

        foreach (var site in results)
        {
            var stars = site.Reviews != null && site.Reviews.Any() ? Math.Round(site.Reviews.Average(r => r.Rating), 1).ToString("F1") : "5.0";
            var acts = string.Join(", ", site.GlampingActivities.Where(ga => ga.Activity != null).Select(a => a.Activity!.Name));
            responseBuilder.AppendLine($"🌲 **{site.Name}** ({site.Region?.Name})");
            responseBuilder.AppendLine($"• **Ціна:** {site.PricePerNight:N0} ₴ / доба | **Рейтинг:** ⭐ {stars}");
            if (!string.IsNullOrEmpty(acts))
            {
                responseBuilder.AppendLine($"• **Зручності:** {acts}");
            }
            responseBuilder.AppendLine($"• [Переглянути деталі та забронювати](/GlampingSites/Details/{site.Id})\n");
        }

        await StreamTextInChunks(responseBuilder.ToString(), sendSse, ct);
    }

    private async Task HandleStatisticsFlow(Func<string, object, Task> sendSse, CancellationToken ct)
    {
        await sendSse("thought", new
        {
            step = 1,
            title = "Аналіз намірів",
            content = "Запит на статистичну аналітику системи."
        });
        await Task.Delay(250, ct);

        await sendSse("tool_call", new
        {
            tool = "get_eco_statistics",
            arguments = new { scope = "all_system" }
        });
        await Task.Delay(300, ct);

        var totalSites = await _context.GlampingSites.CountAsync(ct);
        var avgPrice = totalSites > 0 ? Math.Round(await _context.GlampingSites.AverageAsync(g => (double)g.PricePerNight, ct), 0) : 0;
        var totalReviews = await _context.Reviews.CountAsync(ct);

        await sendSse("tool_result", new
        {
            tool = "get_eco_statistics",
            status = "success",
            totalGlampings = totalSites,
            averagePrice = avgPrice,
            totalReviews = totalReviews
        });
        await Task.Delay(200, ct);

        var sb = new StringBuilder();
        sb.AppendLine("📊 **Актуальна статистика EcoGlamp UA:**\n");
        sb.AppendLine($"• **Всього доступних локацій:** {totalSites}");
        sb.AppendLine($"• **Середня ціна за добу:** {avgPrice:N0} ₴");
        sb.AppendLine($"• **Залишено відгуків:** {totalReviews}\n");
        sb.AppendLine("Детальні інтерактивні графіки доступні на сторінці [Аналітика](/GlampingSites/Analytics)!");

        await StreamTextInChunks(sb.ToString(), sendSse, ct);
    }

    private async Task HandleDetailsFlow(string original, string lower, Func<string, object, Task> sendSse, CancellationToken ct)
    {
        await sendSse("thought", new
        {
            step = 1,
            title = "Пошук сутності",
            content = "Витягую дані про обраний глемпінг..."
        });
        await Task.Delay(250, ct);

        var site = await _context.GlampingSites
            .Include(g => g.Region)
            .Include(g => g.Reviews)
            .Include(g => g.GlampingActivities).ThenInclude(ga => ga.Activity)
            .FirstOrDefaultAsync(g => lower.Contains(g.Name.ToLower()), ct)
            ?? await _context.GlampingSites
                .Include(g => g.Region)
                .Include(g => g.Reviews)
                .Include(g => g.GlampingActivities).ThenInclude(ga => ga.Activity)
                .FirstOrDefaultAsync(ct);

        if (site == null)
        {
            await StreamTextInChunks("На жаль, локацію не знайдено в базі.", sendSse, ct);
            return;
        }

        await sendSse("tool_call", new
        {
            tool = "get_glamping_details",
            arguments = new { siteId = site.Id, siteName = site.Name }
        });
        await Task.Delay(300, ct);

        await sendSse("tool_result", new
        {
            tool = "get_glamping_details",
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
        sb.AppendLine($"• **Телефон:** `{site.ContactPhone}`");
        sb.AppendLine($"• **Ціна за добу:** {site.PricePerNight:N0} ₴");
        sb.AppendLine($"• **Місткість:** до {site.MaxGuests} осіб | **Рейтинг:** ⭐ {avgRating}");
        if (!string.IsNullOrEmpty(acts)) sb.AppendLine($"• **Зручності:** {acts}");
        sb.AppendLine($"\n[Відкрити сторінку локації](/GlampingSites/Details/{site.Id}) | [Показати на мапі](/GlampingSites/Map)");

        await StreamTextInChunks(sb.ToString(), sendSse, ct);
    }

    private async Task HandleAddReviewFlow(string original, string lower, Func<string, object, Task> sendSse, CancellationToken ct)
    {
        await sendSse("thought", new
        {
            step = 1,
            title = "Обробка команди на запис",
            content = "Витягую сутність, автора, оцінку та текст відгуку..."
        });
        await Task.Delay(250, ct);

        var site = await _context.GlampingSites
            .FirstOrDefaultAsync(g => lower.Contains(g.Name.ToLower()), ct)
            ?? await _context.GlampingSites.FirstOrDefaultAsync(ct);

        if (site == null)
        {
            await StreamTextInChunks("Помилка: не знайдено глемпінг для збереження відгуку.", sendSse, ct);
            return;
        }

        int rating = 5;
        var matchRating = Regex.Match(lower, @"(оцінк[а-я]*|на|рейтинг)\s*([1-5])");
        if (matchRating.Success && int.TryParse(matchRating.Groups[2].Value, out var parsedRating))
        {
            rating = parsedRating;
        }

        string author = "Гість EcoGlamp";
        var matchAuthor = Regex.Match(original, @"(від|автор|імені)\s+([A-Za-zА-Яа-яІіЇїЄє]+)");
        if (matchAuthor.Success)
        {
            author = matchAuthor.Groups[2].Value;
        }

        string commentText = original;
        var commentIndex = original.IndexOf(':');
        if (commentIndex >= 0 && commentIndex < original.Length - 1)
        {
            commentText = original.Substring(commentIndex + 1).Trim();
        }

        if (commentText.Length > 300)
        {
            commentText = commentText.Substring(0, 300);
        }

        await sendSse("tool_call", new
        {
            tool = "add_review",
            arguments = new
            {
                siteId = site.Id,
                siteName = site.Name,
                author = author,
                rating = rating,
                comment = commentText
            }
        });
        await Task.Delay(300, ct);

        var newReview = new Review
        {
            GlampingSiteId = site.Id,
            AuthorName = author,
            Rating = rating,
            Comment = commentText,
            CreatedAt = DateTime.UtcNow
        };

        _context.Reviews.Add(newReview);
        await _context.SaveChangesAsync(ct);

        await sendSse("tool_result", new
        {
            tool = "add_review",
            status = "success",
            reviewId = newReview.Id,
            message = $"Відгук збережено до бази даних для '{site.Name}'"
        });
        await Task.Delay(200, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"✅ **Ваш відгук успішно опубліковано!**\n");
        sb.AppendLine($"• **Локація:** {site.Name}");
        sb.AppendLine($"• **Автор:** {author}");
        sb.AppendLine($"• **Оцінка:** {new string('⭐', rating)} ({rating}/5)");
        sb.AppendLine($"• **Коментар:** *«{commentText}»*\n");
        sb.AppendLine($"Ви можете переглянути його на сторінці [Деталі {site.Name}](/GlampingSites/Details/{site.Id}).");

        await StreamTextInChunks(sb.ToString(), sendSse, ct);
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
