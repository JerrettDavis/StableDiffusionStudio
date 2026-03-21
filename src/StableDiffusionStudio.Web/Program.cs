using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Infrastructure.Jobs;
using StableDiffusionStudio.Infrastructure.ModelSources;
using StableDiffusionStudio.Infrastructure.Persistence;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;
using StableDiffusionStudio.Infrastructure.Settings;
using StableDiffusionStudio.Infrastructure.Services;
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

// App paths
builder.Services.AddSingleton<IAppPaths, AppPaths>();
var appPaths = new AppPaths();

// EF Core + SQLite
// Support test isolation via environment variable override
var dbPath = Environment.GetEnvironmentVariable("SDS_TEST_DB_PATH")
    ?? Path.Combine(appPaths.DatabaseDirectory, "studio.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"DataSource={dbPath}"));

// Application services
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IProjectService, ProjectService>();

// Preset services
builder.Services.AddScoped<IPresetRepository, PresetRepository>();
builder.Services.AddScoped<IPresetService, PresetService>();

// Prompt history
builder.Services.AddScoped<IPromptHistoryRepository, PromptHistoryRepository>();
builder.Services.AddScoped<IPromptHistoryService, PromptHistoryService>();

// Data management
builder.Services.AddScoped<IDataManagementService, DataManagementService>();

// Settings
builder.Services.AddScoped<ISettingsProvider, DbSettingsProvider>();
builder.Services.AddScoped<IInferenceSettingsProvider, DbInferenceSettingsProvider>();

// Model services
builder.Services.AddScoped<IStorageRootProvider, DbStorageRootProvider>();
builder.Services.AddScoped<IModelCatalogRepository, ModelCatalogRepository>();
builder.Services.AddScoped<IModelCatalogService, ModelCatalogService>();

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

// Content safety
builder.Services.AddScoped<IContentSafetyService, NsfwSpyContentSafetyService>();

// Generation services
builder.Services.AddScoped<IGenerationService, GenerationService>();
builder.Services.AddScoped<IGenerationJobRepository, GenerationJobRepository>();
builder.Services.AddSingleton<MockInferenceBackend>();
builder.Services.AddSingleton<StableDiffusionCppBackend>();
// Backend selection: try real SD.NET first, fall back to mock
// Deferred check — don't evaluate at DI build time, check on first use
builder.Services.AddSingleton<IInferenceBackend>(sp =>
{
    var sdCpp = sp.GetRequiredService<StableDiffusionCppBackend>();
    var logger = sp.GetRequiredService<ILogger<StableDiffusionCppBackend>>();
    try
    {
        var available = sdCpp.IsAvailableAsync().GetAwaiter().GetResult();
        logger.LogInformation("StableDiffusion.NET backend available: {Available}", available);
        if (available) return sdCpp;
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to check StableDiffusion.NET availability");
    }
    logger.LogInformation("Using MockInferenceBackend");
    return sp.GetRequiredService<MockInferenceBackend>();
});
builder.Services.AddKeyedScoped<IJobHandler, GenerationJobHandler>("generation");

// Telemetry
builder.Services.AddSingleton<StudioMetrics>();

// Database health check
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

// SignalR
builder.Services.AddSignalR();
builder.Services.AddScoped<IGenerationNotifier, SignalRGenerationNotifier>();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.MapDefaultEndpoints();

// Force backend resolution at startup to get logging
var backend = app.Services.GetRequiredService<IInferenceBackend>();
app.Logger.LogInformation("Active inference backend: {Backend} ({Id})", backend.DisplayName, backend.BackendId);

// Database initialization with robust migration handling
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // Check if this is a fresh database (no tables exist)
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name != '__EFMigrationsHistory'";
        var tableCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        await conn.CloseAsync();

        if (tableCount == 0)
        {
            // Fresh database — create schema from scratch
            logger.LogInformation("Fresh database detected — creating schema");
            await db.Database.EnsureCreatedAsync();

            // Mark all migrations as applied (we just created the full schema)
            var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
            foreach (var migration in pendingMigrations)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1})",
                    migration, "10.0.5");
            }

            logger.LogInformation("Schema created successfully");
        }
        else
        {
            // Existing database — apply any pending migrations
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count > 0)
            {
                logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                    pending.Count, string.Join(", ", pending));
            }
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migration complete");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed");

        // Last resort: try to apply schema changes manually for SQLite
        // SQLite doesn't support all ALTER TABLE operations through EF migrations well,
        // so we do it manually for known missing columns/tables
        try
        {
            logger.LogWarning("Attempting manual schema repair...");
            await RepairSchema(db, logger);
        }
        catch (Exception repairEx)
        {
            logger.LogError(repairEx, "Manual schema repair failed — deleting and recreating database");
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            logger.LogWarning("Database recreated from scratch — previous data was lost");
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();

// Serve generated image assets from the local app data directory
Directory.CreateDirectory(appPaths.AssetsDirectory);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(appPaths.AssetsDirectory),
    RequestPath = "/assets"
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapHub<StudioHub>("/hubs/studio");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Make Program accessible for integration testing
public partial class Program
{
    static async Task RepairSchema(AppDbContext db, ILogger logger)
    {
        // Attempt to add missing tables and columns for SQLite compatibility
        var repairs = new (string TableName, string CreateSql)[]
        {
            ("GenerationJobs", @"CREATE TABLE IF NOT EXISTS ""GenerationJobs"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_GenerationJobs"" PRIMARY KEY,
                ""ProjectId"" TEXT NOT NULL,
                ""Parameters"" TEXT NOT NULL DEFAULT '{}',
                ""Status"" TEXT NOT NULL DEFAULT 'Pending',
                ""CreatedAt"" INTEGER NOT NULL DEFAULT 0,
                ""StartedAt"" INTEGER,
                ""CompletedAt"" INTEGER,
                ""ErrorMessage"" TEXT
            )"),
            ("GeneratedImages", @"CREATE TABLE IF NOT EXISTS ""GeneratedImages"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_GeneratedImages"" PRIMARY KEY,
                ""GenerationJobId"" TEXT NOT NULL,
                ""FilePath"" TEXT NOT NULL DEFAULT '',
                ""Seed"" INTEGER NOT NULL DEFAULT 0,
                ""Width"" INTEGER NOT NULL DEFAULT 0,
                ""Height"" INTEGER NOT NULL DEFAULT 0,
                ""GenerationTimeSeconds"" REAL NOT NULL DEFAULT 0,
                ""ParametersJson"" TEXT NOT NULL DEFAULT '',
                ""CreatedAt"" INTEGER NOT NULL DEFAULT 0,
                ""IsFavorite"" INTEGER NOT NULL DEFAULT 0,
                ""ContentRating"" TEXT NOT NULL DEFAULT 'Unknown',
                ""NsfwScore"" REAL NOT NULL DEFAULT 0,
                ""IsRevealed"" INTEGER NOT NULL DEFAULT 0,
                CONSTRAINT ""FK_GeneratedImages_GenerationJobs_GenerationJobId"" FOREIGN KEY (""GenerationJobId"") REFERENCES ""GenerationJobs"" (""Id"") ON DELETE CASCADE
            )"),
            ("GenerationPresets", @"CREATE TABLE IF NOT EXISTS ""GenerationPresets"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_GenerationPresets"" PRIMARY KEY,
                ""Name"" TEXT NOT NULL DEFAULT '',
                ""Description"" TEXT,
                ""AssociatedModelId"" TEXT,
                ""ModelFamilyFilter"" TEXT,
                ""IsDefault"" INTEGER NOT NULL DEFAULT 0,
                ""CreatedAt"" INTEGER NOT NULL DEFAULT 0,
                ""UpdatedAt"" INTEGER NOT NULL DEFAULT 0,
                ""PositivePromptTemplate"" TEXT,
                ""NegativePrompt"" TEXT NOT NULL DEFAULT '',
                ""Sampler"" TEXT NOT NULL DEFAULT 'EulerA',
                ""Scheduler"" TEXT NOT NULL DEFAULT 'Normal',
                ""Steps"" INTEGER NOT NULL DEFAULT 20,
                ""CfgScale"" REAL NOT NULL DEFAULT 7.0,
                ""Width"" INTEGER NOT NULL DEFAULT 512,
                ""Height"" INTEGER NOT NULL DEFAULT 512,
                ""BatchSize"" INTEGER NOT NULL DEFAULT 1,
                ""ClipSkip"" INTEGER NOT NULL DEFAULT 1
            )"),
            ("PromptHistories", @"CREATE TABLE IF NOT EXISTS ""PromptHistories"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_PromptHistories"" PRIMARY KEY,
                ""PositivePrompt"" TEXT NOT NULL DEFAULT '',
                ""NegativePrompt"" TEXT NOT NULL DEFAULT '',
                ""UsedAt"" INTEGER NOT NULL DEFAULT 0,
                ""UseCount"" INTEGER NOT NULL DEFAULT 0
            )"),
        };

        foreach (var (tableName, createSql) in repairs)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(createSql);
                logger.LogInformation("Ensured table exists: {Table}", tableName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to create table {Table}", tableName);
            }
        }

        // Add missing columns to existing tables (SQLite supports ADD COLUMN)
        var columns = new (string Table, string Column, string AlterSql)[]
        {
            ("ModelRecords", "Type", @"ALTER TABLE ""ModelRecords"" ADD COLUMN ""Type"" TEXT NOT NULL DEFAULT 'Checkpoint'"),
            ("GeneratedImages", "IsFavorite", @"ALTER TABLE ""GeneratedImages"" ADD COLUMN ""IsFavorite"" INTEGER NOT NULL DEFAULT 0"),
            ("GeneratedImages", "ContentRating", @"ALTER TABLE ""GeneratedImages"" ADD COLUMN ""ContentRating"" TEXT NOT NULL DEFAULT 'Unknown'"),
            ("GeneratedImages", "NsfwScore", @"ALTER TABLE ""GeneratedImages"" ADD COLUMN ""NsfwScore"" REAL NOT NULL DEFAULT 0"),
            ("GeneratedImages", "IsRevealed", @"ALTER TABLE ""GeneratedImages"" ADD COLUMN ""IsRevealed"" INTEGER NOT NULL DEFAULT 0"),
        };

        foreach (var (table, column, alterSql) in columns)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(alterSql);
                logger.LogInformation("Added column {Table}.{Column}", table, column);
            }
            catch (Exception ex) when (ex.Message.Contains("duplicate column"))
            {
                // Column already exists — that's fine
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to add column {Table}.{Column}", table, column);
            }
        }
    }
}
