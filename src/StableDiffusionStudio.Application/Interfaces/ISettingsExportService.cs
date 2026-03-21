namespace StableDiffusionStudio.Application.Interfaces;

/// <summary>
/// Exports and imports all application settings as a JSON document.
/// </summary>
public interface ISettingsExportService
{
    /// <summary>
    /// Exports all settings to a JSON string.
    /// </summary>
    Task<string> ExportAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Imports settings from a JSON string, overwriting existing values.
    /// </summary>
    Task ImportAllAsync(string json, CancellationToken ct = default);
}
