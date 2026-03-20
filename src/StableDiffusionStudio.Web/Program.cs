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
using StableDiffusionStudio.Infrastructure.Inference;
using StableDiffusionStudio.Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;
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
builder.Services.AddScoped<ModelCatalogService>();

// Model providers
builder.Services.AddScoped<IModelProvider, LocalFolderProvider>();
builder.Services.AddScoped<IModelProvider, HuggingFaceProvider>();
builder.Services.AddScoped<IModelProvider, CivitAIProvider>();

// Provider infrastructure
builder.Services.AddScoped<IProviderCredentialStore, ProviderCredentialStore>();
builder.Services.AddScoped<HttpDownloadClient>();
builder.Services.AddHttpClient<HuggingFaceProvider>();
builder.Services.AddHttpClient<CivitAIProvider>();

// Background job system
builder.Services.AddSingleton<JobChannel>();
builder.Services.AddScoped<ChannelJobQueue>();
builder.Services.AddScoped<IJobQueue>(sp => sp.GetRequiredService<ChannelJobQueue>());
builder.Services.AddHostedService<BackgroundJobProcessor>();
builder.Services.AddKeyedScoped<IJobHandler, ModelScanJobHandler>("model-scan");
builder.Services.AddKeyedScoped<IJobHandler, ModelDownloadJobHandler>("model-download");

// Generation services
builder.Services.AddScoped<GenerationService>();
builder.Services.AddScoped<IGenerationJobRepository, GenerationJobRepository>();
builder.Services.AddSingleton<MockInferenceBackend>();
builder.Services.AddSingleton<StableDiffusionCppBackend>();
builder.Services.AddSingleton<IInferenceBackend>(sp =>
{
    var sdCpp = sp.GetRequiredService<StableDiffusionCppBackend>();
    if (sdCpp.IsAvailableAsync().GetAwaiter().GetResult())
        return sdCpp;
    return sp.GetRequiredService<MockInferenceBackend>();
});
builder.Services.AddKeyedScoped<IJobHandler, GenerationJobHandler>("generation");

// Telemetry
builder.Services.AddSingleton<StudioMetrics>();

// SignalR
builder.Services.AddSignalR();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.MapDefaultEndpoints();

// Auto-create/update schema on startup
// EnsureCreated doesn't update existing DBs, so we delete and recreate if schema is stale
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        // Test if the schema is current by querying a column from the latest migration
        await db.Database.ExecuteSqlRawAsync("SELECT Type FROM ModelRecords LIMIT 0");
        await db.Database.ExecuteSqlRawAsync("SELECT Id FROM GenerationJobs LIMIT 0");
    }
    catch
    {
        // Schema is stale — recreate
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();

// Serve generated image assets from the local app data directory
var assetsPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "StableDiffusionStudio", "Assets");
Directory.CreateDirectory(assetsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(assetsPath),
    RequestPath = "/assets"
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapHub<StudioHub>("/hubs/studio");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Make Program accessible for integration testing
public partial class Program { }
