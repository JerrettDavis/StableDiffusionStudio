using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Web.Mcp.Tools;

[McpServerToolType]
public class ModelRuleTools
{
    [McpServerTool(Name = "get_model_rules"), Description(
        "Get the resolved generation rules for a checkpoint (cascaded from app defaults → family → checkpoint). " +
        "Returns the effective settings that would be applied when generating with this model.")]
    public static async Task<string> GetModelRules(
        IModelRuleService ruleService,
        [Description("Checkpoint GUID to resolve rules for")] string checkpointId,
        [Description("Optional family hint (e.g. 'SD 1.5', 'Flux.1 D', 'Pony')")] string? family = null)
    {
        if (!Guid.TryParse(checkpointId, out var id))
            return JsonSerializer.Serialize(new { error = "Invalid checkpoint ID" });

        var rules = await ruleService.ResolveAsync(id, family);
        return JsonSerializer.Serialize(new
        {
            checkpointId = id,
            resolvedRules = new
            {
                rules.Steps, rules.CfgScale, sampler = rules.Sampler?.ToString(),
                scheduler = rules.Scheduler?.ToString(), rules.ClipSkip, rules.Eta,
                rules.Width, rules.Height, rules.DenoisingStrength,
                rules.HiresFixEnabled, rules.HiresUpscaleFactor, rules.HiresSteps,
                rules.HiresDenoisingStrength, rules.DefaultNegativePrompt
            }
        });
    }

    [McpServerTool(Name = "set_family_rule"), Description(
        "Set generation defaults for an entire model family (e.g. 'Pony', 'Flux.1 D', 'SDXL 1.0'). " +
        "Only set values you want to override — null values inherit from app defaults.")]
    public static async Task<string> SetFamilyRule(
        IModelRuleService ruleService,
        [Description("Family name (e.g. 'SD 1.5', 'Flux.1 D', 'Pony', 'Illustrious')")] string family,
        [Description("Steps (1-150)")] int? steps = null,
        [Description("CFG Scale (0-30)")] double? cfgScale = null,
        [Description("Sampler: Euler, EulerA, DPMPlusPlus2M, DDIM, etc.")] string? sampler = null,
        [Description("Width (multiple of 64)")] int? width = null,
        [Description("Height (multiple of 64)")] int? height = null,
        [Description("Clip Skip (1-12)")] int? clipSkip = null,
        [Description("Default negative prompt")] string? negativePrompt = null)
    {
        var parsedSampler = sampler is not null && Enum.TryParse<Domain.Enums.Sampler>(sampler, true, out var s)
            ? s : (Domain.Enums.Sampler?)null;

        var rule = new ModelRuleSet
        {
            Steps = steps, CfgScale = cfgScale, Sampler = parsedSampler,
            Width = width, Height = height, ClipSkip = clipSkip,
            DefaultNegativePrompt = negativePrompt
        };

        await ruleService.SetFamilyRuleAsync(family, rule);
        return JsonSerializer.Serialize(new { family, message = $"Rule saved for family '{family}'" });
    }

    [McpServerTool(Name = "set_checkpoint_rule"), Description(
        "Set generation defaults for a specific checkpoint model. Overrides family rules for this model.")]
    public static async Task<string> SetCheckpointRule(
        IModelRuleService ruleService,
        [Description("Checkpoint GUID")] string checkpointId,
        [Description("Steps")] int? steps = null,
        [Description("CFG Scale")] double? cfgScale = null,
        [Description("Sampler")] string? sampler = null,
        [Description("Width")] int? width = null,
        [Description("Height")] int? height = null,
        [Description("Clip Skip")] int? clipSkip = null,
        [Description("Default negative prompt")] string? negativePrompt = null)
    {
        if (!Guid.TryParse(checkpointId, out var id))
            return JsonSerializer.Serialize(new { error = "Invalid checkpoint ID" });

        var parsedSampler = sampler is not null && Enum.TryParse<Domain.Enums.Sampler>(sampler, true, out var s)
            ? s : (Domain.Enums.Sampler?)null;

        var rule = new ModelRuleSet
        {
            Steps = steps, CfgScale = cfgScale, Sampler = parsedSampler,
            Width = width, Height = height, ClipSkip = clipSkip,
            DefaultNegativePrompt = negativePrompt
        };

        await ruleService.SetCheckpointRuleAsync(id, rule);
        return JsonSerializer.Serialize(new { checkpointId = id, message = "Checkpoint rule saved" });
    }

    [McpServerTool(Name = "list_model_rules"), Description(
        "List all configured model rules (both family rules and checkpoint-specific rules).")]
    public static async Task<string> ListModelRules(
        IModelRuleService ruleService)
    {
        var familyRules = await ruleService.ListFamilyRulesAsync();
        var checkpointRules = await ruleService.ListCheckpointRulesAsync();

        return JsonSerializer.Serialize(new
        {
            familyRules = familyRules.ToDictionary(
                kv => kv.Key,
                kv => new { kv.Value.Steps, kv.Value.CfgScale, sampler = kv.Value.Sampler?.ToString(),
                    kv.Value.Width, kv.Value.Height, kv.Value.ClipSkip }),
            checkpointRules = checkpointRules.ToDictionary(
                kv => kv.Key.ToString(),
                kv => new { kv.Value.Steps, kv.Value.CfgScale, sampler = kv.Value.Sampler?.ToString(),
                    kv.Value.Width, kv.Value.Height, kv.Value.ClipSkip }),
            familyCount = familyRules.Count,
            checkpointCount = checkpointRules.Count
        });
    }

    [McpServerTool(Name = "list_known_families"), Description(
        "List all known model family identifiers (from CivitAI base models + custom).")]
    public static async Task<string> ListKnownFamilies(
        IModelRuleService ruleService)
    {
        var families = await ruleService.GetKnownFamiliesAsync();
        return JsonSerializer.Serialize(new { families, count = families.Count });
    }
}
