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
builder.Services.AddScoped<IFluxComponentResolver, FluxComponentResolver>();

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
// Backend selection: lazy wrapper that checks on first use, never blocks startup
builder.Services.AddSingleton<IInferenceBackend, LazyInferenceBackend>();
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

// Database initialization — ensure schema is up to date
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
            // Fresh database — create full schema from the EF model
            logger.LogInformation("Fresh database detected — creating schema");
            await db.Database.EnsureCreatedAsync();
            logger.LogInformation("Schema created successfully");
        }
        else
        {
            // Existing database — ensure all tables and columns exist
            // SQLite doesn't support full ALTER TABLE, so we use CREATE TABLE IF NOT EXISTS
            // and ADD COLUMN with graceful duplicate handling
            logger.LogInformation("Existing database detected — ensuring schema is up to date");
            await RepairSchema(db, logger);
            logger.LogInformation("Schema repair complete");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed — recreating from scratch");
        try
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            logger.LogWarning("Database recreated — previous data was lost");
        }
        catch (Exception fatalEx)
        {
            logger.LogCritical(fatalEx, "Cannot create database — app may not function correctly");
        }
    }
}

// Clean up stale jobs from previous sessions
// Jobs left in Pending/Running state after a restart will never complete
using (var scope = app.Services.CreateScope())
{
    var cleanupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Mark stale generation jobs as Failed
        var staleGenJobs = await db.GenerationJobs
            .Where(j => j.Status == Domain.Enums.GenerationJobStatus.Pending
                     || j.Status == Domain.Enums.GenerationJobStatus.Running)
            .ToListAsync();
        foreach (var job in staleGenJobs)
        {
            job.Fail("Cancelled — app was restarted");
        }

        // Mark stale background jobs as Failed
        var staleJobRecords = await db.JobRecords
            .Where(j => j.Status == Domain.Enums.JobStatus.Pending
                     || j.Status == Domain.Enums.JobStatus.Running)
            .ToListAsync();
        foreach (var job in staleJobRecords)
        {
            job.Fail("Cancelled — app was restarted");
        }

        if (staleGenJobs.Count > 0 || staleJobRecords.Count > 0)
        {
            await db.SaveChangesAsync();
            cleanupLogger.LogInformation("Cleaned up {GenJobs} stale generation job(s) and {JobRecords} stale background job(s)",
                staleGenJobs.Count, staleJobRecords.Count);
        }
    }
    catch (Exception ex)
    {
        cleanupLogger.LogWarning(ex, "Failed to clean up stale jobs");
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
                ""Parameters"" TEXT NOT NULL DEFAULT '',
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
                // Check if column already exists before trying to add it
                var checkConn = db.Database.GetDbConnection();
                if (checkConn.State != System.Data.ConnectionState.Open)
                    await checkConn.OpenAsync();
                using var checkCmd = checkConn.CreateCommand();
                checkCmd.CommandText = $"SELECT count(*) FROM pragma_table_info('{table}') WHERE name='{column}'";
                var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

                if (exists)
                {
                    logger.LogDebug("Column {Table}.{Column} already exists — skipping", table, column);
                    continue;
                }

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
