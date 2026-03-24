using System.Text.Json;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.ModelSources;

namespace StableDiffusionStudio.Infrastructure.Services;

public class ModelRuleService : IModelRuleService
{
    private readonly ISettingsProvider _settings;
    private readonly IModelCatalogRepository _modelRepo;
    private readonly ILogger<ModelRuleService> _logger;

    private const string FamilyRulesKey = "ModelRules:families";
    private const string CheckpointRulesKey = "ModelRules:checkpoints";
    private const string CustomFamiliesKey = "ModelRules:customFamilies";

    public ModelRuleService(ISettingsProvider settings, IModelCatalogRepository modelRepo,
        ILogger<ModelRuleService> logger)
    {
        _settings = settings;
        _modelRepo = modelRepo;
        _logger = logger;
    }

    public async Task<ModelRuleSet> ResolveAsync(Guid checkpointId, string? familyHint = null, CancellationToken ct = default)
    {
        var resolved = ModelRuleSet.AppDefaults;

        // Determine family — from hint or from the model record
        var family = familyHint;
        if (string.IsNullOrEmpty(family))
        {
            var model = await _modelRepo.GetByIdAsync(checkpointId, ct);
            if (model is not null)
            {
                // Use the model's family enum as a string — but also check CivitAI base model metadata
                family = model.ModelFamily.ToString();
                // If Unknown, try to infer from the model's compatibility hints or tags
                if (family == "Unknown" && model.CompatibilityHints is not null)
                    family = model.CompatibilityHints;
            }
        }

        // Layer 2: Family rule
        if (!string.IsNullOrEmpty(family))
        {
            var familyRule = await GetFamilyRuleAsync(family, ct);
            if (familyRule is not null)
                resolved = familyRule.MergeOver(resolved);
        }

        // Layer 3: Checkpoint-specific rule
        var checkpointRule = await GetCheckpointRuleAsync(checkpointId, ct);
        if (checkpointRule is not null)
            resolved = checkpointRule.MergeOver(resolved);

        return resolved;
    }

    public async Task<ModelRuleSet?> GetFamilyRuleAsync(string family, CancellationToken ct = default)
    {
        var allRules = await LoadFamilyRulesAsync(ct);
        return allRules.TryGetValue(NormalizeFamily(family), out var rule) ? rule : null;
    }

    public async Task<ModelRuleSet?> GetCheckpointRuleAsync(Guid checkpointId, CancellationToken ct = default)
    {
        var allRules = await LoadCheckpointRulesAsync(ct);
        return allRules.TryGetValue(checkpointId, out var rule) ? rule : null;
    }

    public async Task SetFamilyRuleAsync(string family, ModelRuleSet rule, CancellationToken ct = default)
    {
        var allRules = await LoadFamilyRulesAsync(ct);
        allRules[NormalizeFamily(family)] = rule;
        await SaveFamilyRulesAsync(allRules, ct);
        _logger.LogInformation("Saved model rule for family '{Family}'", family);
    }

    public async Task SetCheckpointRuleAsync(Guid checkpointId, ModelRuleSet rule, CancellationToken ct = default)
    {
        var allRules = await LoadCheckpointRulesAsync(ct);
        allRules[checkpointId] = rule;
        await SaveCheckpointRulesAsync(allRules, ct);
        _logger.LogInformation("Saved model rule for checkpoint {CheckpointId}", checkpointId);
    }

    public async Task DeleteFamilyRuleAsync(string family, CancellationToken ct = default)
    {
        var allRules = await LoadFamilyRulesAsync(ct);
        allRules.Remove(NormalizeFamily(family));
        await SaveFamilyRulesAsync(allRules, ct);
    }

    public async Task DeleteCheckpointRuleAsync(Guid checkpointId, CancellationToken ct = default)
    {
        var allRules = await LoadCheckpointRulesAsync(ct);
        allRules.Remove(checkpointId);
        await SaveCheckpointRulesAsync(allRules, ct);
    }

    public async Task<IReadOnlyDictionary<string, ModelRuleSet>> ListFamilyRulesAsync(CancellationToken ct = default)
    {
        return await LoadFamilyRulesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, ModelRuleSet>> ListCheckpointRulesAsync(CancellationToken ct = default)
    {
        return await LoadCheckpointRulesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetKnownFamiliesAsync(CancellationToken ct = default)
    {
        var builtIn = CivitAIProvider.AllBaseModels.ToList();
        var custom = await LoadCustomFamiliesAsync(ct);
        foreach (var f in custom)
        {
            if (!builtIn.Contains(f, StringComparer.OrdinalIgnoreCase))
                builtIn.Add(f);
        }
        builtIn.Sort(StringComparer.OrdinalIgnoreCase);
        return builtIn;
    }

    public async Task AddCustomFamilyAsync(string family, CancellationToken ct = default)
    {
        var custom = await LoadCustomFamiliesAsync(ct);
        var normalized = family.Trim();
        if (!custom.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            custom.Add(normalized);
            await _settings.SetAsync(CustomFamiliesKey, custom, ct);
        }
    }

    // --- Persistence helpers ---

    private async Task<Dictionary<string, ModelRuleSet>> LoadFamilyRulesAsync(CancellationToken ct)
    {
        return await _settings.GetAsync<Dictionary<string, ModelRuleSet>>(FamilyRulesKey, ct)
               ?? new();
    }

    private async Task SaveFamilyRulesAsync(Dictionary<string, ModelRuleSet> rules, CancellationToken ct)
    {
        await _settings.SetAsync(FamilyRulesKey, rules, ct);
    }

    private async Task<Dictionary<Guid, ModelRuleSet>> LoadCheckpointRulesAsync(CancellationToken ct)
    {
        return await _settings.GetAsync<Dictionary<Guid, ModelRuleSet>>(CheckpointRulesKey, ct)
               ?? new();
    }

    private async Task SaveCheckpointRulesAsync(Dictionary<Guid, ModelRuleSet> rules, CancellationToken ct)
    {
        await _settings.SetAsync(CheckpointRulesKey, rules, ct);
    }

    private async Task<List<string>> LoadCustomFamiliesAsync(CancellationToken ct)
    {
        return await _settings.GetAsync<List<string>>(CustomFamiliesKey, ct) ?? new();
    }

    private static string NormalizeFamily(string family) => family.Trim();
}
