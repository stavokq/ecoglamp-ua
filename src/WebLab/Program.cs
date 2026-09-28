using Microsoft.EntityFrameworkCore;
using WebLab.Data;
using WebLab.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddScoped<IFileService, FileService>();

var provider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "PostgreSql";
var pgConn = builder.Configuration.GetConnectionString("DefaultConnection");
var sqliteConn = builder.Configuration.GetConnectionString("SqliteConnection") ?? "Data Source=glamping.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(pgConn))
    {
        options.UseSqlite(sqliteConn);
    }
    else
    {
        options.UseNpgsql(pgConn);
    }
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var maxRetries = 5;
    var delay = TimeSpan.FromSeconds(2);

    for (int retry = 1; retry <= maxRetries; retry++)
    {
        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();
            await DbInitializer.SeedDataAsync(context);
            logger.LogInformation("Database initialized.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database connection attempt {Retry}/{Max} failed. Waiting {Delay}s...", retry, maxRetries, delay.TotalSeconds);
            if (retry == maxRetries)
            {
                logger.LogError(ex, "Failed to initialize database after {Max} attempts.", maxRetries);
            }
            else
            {
                Thread.Sleep(delay);
            }
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=GlampingSites}/{action=Index}/{id?}");

app.Run();
