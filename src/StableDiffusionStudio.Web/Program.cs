using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Infrastructure.Jobs;
using StableDiffusionStudio.Infrastructure.ModelSources;
using StableDiffusionStudio.Infrastructure.Persistence;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;
using StableDiffusionStudio.Infrastructure.Settings;
using StableDiffusionStudio.Infrastructure.Telemetry;
using StableDiffusionStudio.Infrastructure.Storage;
using StableDiffusionStudio.Web.Components;
using StableDiffusionStudio.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// MudBlazor
builder.Services.AddMudServices();

// EF Core + SQLite
// Support test isolation via environment variable override
var dbPath = Environment.GetEnvironmentVariable("SDS_TEST_DB_PATH")
    ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StableDiffusionStudio", "Database", "studio.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"DataSource={dbPath}"));

// Application services
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ProjectService>();

// Settings
builder.Services.AddScoped<ISettingsProvider, DbSettingsProvider>();

// Model services
builder.Services.AddScoped<IStorageRootProvider, DbStorageRootProvider>();
builder.Services.AddScoped<IModelCatalogRepository, ModelCatalogRepository>();
builder.Services.AddScoped<IModelProvider, LocalFolderProvider>();
builder.Services.AddScoped<ModelCatalogService>();

// Background job system
builder.Services.AddSingleton<JobChannel>();
builder.Services.AddScoped<ChannelJobQueue>();
builder.Services.AddScoped<IJobQueue>(sp => sp.GetRequiredService<ChannelJobQueue>());
builder.Services.AddHostedService<BackgroundJobProcessor>();
builder.Services.AddKeyedScoped<IJobHandler, ModelScanJobHandler>("model-scan");

// Telemetry
builder.Services.AddSingleton<StudioMetrics>();

// SignalR
builder.Services.AddSignalR();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.MapDefaultEndpoints();

// Auto-create schema on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHub<StudioHub>("/hubs/studio");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Make Program accessible for integration testing
public partial class Program { }
