using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Web.Mcp.Tools;

[McpServerToolType]
public class PresetTools
{
    [McpServerTool(Name = "list_presets"), Description(
        "List saved generation presets. Presets store reusable generation settings (sampler, steps, CFG, dimensions, etc.).")]
    public static async Task<string> ListPresets(
        IPresetRepository presetRepository,
        [Description("Filter by model family: SD15, SDXL, Flux, Pony")] string? family = null)
    {
        var familyFilter = family is not null && Enum.TryParse<Domain.Enums.ModelFamily>(family, true, out var f)
            ? f : (Domain.Enums.ModelFamily?)null;

        var presets = await presetRepository.ListAsync(null, familyFilter);
        return JsonSerializer.Serialize(new
        {
            presets = presets.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                description = p.Description,
                sampler = p.Sampler.ToString(),
                steps = p.Steps,
                cfgScale = p.CfgScale,
                width = p.Width,
                height = p.Height,
                clipSkip = p.ClipSkip,
                isDefault = p.IsDefault,
                modelFamily = p.ModelFamilyFilter?.ToString()
            }),
            count = presets.Count
        });
    }

    [McpServerTool(Name = "get_settings"), Description(
        "Read application settings by key. Common keys: 'InferenceSettings', 'OutputSettings', 'ModelEnrichmentSettings', 'DownloadLocations'.")]
    public static async Task<string> GetSettings(
        ISettingsProvider settingsProvider,
        [Description("Settings key to read")] string key)
    {
        var raw = await settingsProvider.GetRawAsync(key);
        if (raw is null)
            return JsonSerializer.Serialize(new { key, value = (string?)null, message = "Setting not found" });

        return JsonSerializer.Serialize(new { key, value = raw });
    }

    [McpServerTool(Name = "update_settings"), Description(
        "Update application settings. Pass the settings key and a JSON value string.")]
    public static async Task<string> UpdateSettings(
        ISettingsProvider settingsProvider,
        [Description("Settings key to update")] string key,
        [Description("JSON value to set")] string valueJson)
    {
        await settingsProvider.SetRawAsync(key, valueJson);
        return JsonSerializer.Serialize(new { key, message = "Settings updated successfully" });
    }
}
