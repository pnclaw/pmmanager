using Microsoft.EntityFrameworkCore;
using pmm.Api.Features.DownloadClients;
using pmm.Api.Features.Indexers.Scraping;
using Pmm.Database;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

var logsPath = Path.GetFullPath(
    Environment.GetEnvironmentVariable("LOGS_PATH") ?? "logs/app-.log");

Directory.CreateDirectory(Path.GetDirectoryName(logsPath)!);

// Serilog — reads MinimumLevel from appsettings, writes to Console + rolling File
builder.Host.UseSerilog((ctx, _, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: logsPath,
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IndexerScrapeService>();
builder.Services.AddScoped<pmm.Api.Features.Prdb.Sync.PrdbSyncService>();
builder.Services.AddScoped<pmm.Api.Features.Prdb.PrdbFavoritesService>();
builder.Services.AddScoped<pmm.Api.Features.Prdb.Sync.PrdbActorSyncService>();
builder.Services.AddScoped<pmm.Api.Features.Prdb.Sync.PrdbVideoDetailSyncService>();
builder.Services.AddHostedService<IndexerScraperBackgroundService>();
builder.Services.AddScoped<DownloadClientTester>();
builder.Services.AddScoped<DownloadClientSender>();
builder.Services.AddHostedService<pmm.Api.Background.SyncWorker>();

// EF Core / SQLite — DB_PATH env var takes precedence over appsettings
var dbPath = Path.GetFullPath(
    Environment.GetEnvironmentVariable("DB_PATH")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "./data/app.db");

Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// CORS — only active in Development to allow the Vite dev server on port 5173
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()));
}

var app = builder.Build();

// Apply any pending EF Core migrations on startup
app.Services.MigrateDatabase();

// Serilog HTTP request logging
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseCors();
}

// Serve the Vue SPA from wwwroot/
app.UseStaticFiles();

// API routes must be mapped before the SPA fallback
app.MapControllers();

// Fallback to index.html for client-side routing (History Mode)
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
