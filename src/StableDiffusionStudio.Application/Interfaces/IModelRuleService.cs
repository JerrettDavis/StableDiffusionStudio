using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Interfaces;

/// <summary>
/// Manages model generation rules — cascading defaults from app → family → checkpoint.
/// Family identifiers are free-form strings (e.g. "SD 1.5", "Flux.1 D", "Pony").
/// </summary>
public interface IModelRuleService
{
    /// <summary>
    /// Resolve the effective rule set for a specific checkpoint, applying the cascade:
    /// App Defaults → Family Rule (if exists) → Checkpoint Rule (if exists).
    /// </summary>
    Task<ModelRuleSet> ResolveAsync(Guid checkpointId, string? familyHint = null, CancellationToken ct = default);

    /// <summary>Get the rule set for a specific model family (null if not configured).</summary>
    Task<ModelRuleSet?> GetFamilyRuleAsync(string family, CancellationToken ct = default);

    /// <summary>Get the rule set for a specific checkpoint (null if not configured).</summary>
    Task<ModelRuleSet?> GetCheckpointRuleAsync(Guid checkpointId, CancellationToken ct = default);

    /// <summary>Save a rule set for a model family.</summary>
    Task SetFamilyRuleAsync(string family, ModelRuleSet rule, CancellationToken ct = default);

    /// <summary>Save a rule set for a specific checkpoint.</summary>
    Task SetCheckpointRuleAsync(Guid checkpointId, ModelRuleSet rule, CancellationToken ct = default);

    /// <summary>Delete a family rule.</summary>
    Task DeleteFamilyRuleAsync(string family, CancellationToken ct = default);

    /// <summary>Delete a checkpoint rule.</summary>
    Task DeleteCheckpointRuleAsync(Guid checkpointId, CancellationToken ct = default);

    /// <summary>List all configured family rules.</summary>
    Task<IReadOnlyDictionary<string, ModelRuleSet>> ListFamilyRulesAsync(CancellationToken ct = default);

    /// <summary>List all configured checkpoint rules (returns checkpoint ID → rule).</summary>
    Task<IReadOnlyDictionary<Guid, ModelRuleSet>> ListCheckpointRulesAsync(CancellationToken ct = default);

    /// <summary>Get the list of all known model families (from CivitAI + custom).</summary>
    Task<IReadOnlyList<string>> GetKnownFamiliesAsync(CancellationToken ct = default);

    /// <summary>Add a custom family identifier.</summary>
    Task AddCustomFamilyAsync(string family, CancellationToken ct = default);
}
