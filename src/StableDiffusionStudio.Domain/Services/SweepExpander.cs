using System.Text.Json;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Services;

/// <summary>
/// Represents a single combination produced by expanding sweep axes over base generation parameters.
/// </summary>
/// <param name="Parameters">The fully resolved <see cref="GenerationParameters"/> for this combination.</param>
/// <param name="GridX">Column index in the sweep grid (corresponds to axis 1).</param>
/// <param name="GridY">Row index in the sweep grid (corresponds to axis 2, always 0 for single-axis sweeps).</param>
/// <param name="AxisValues">Dictionary of parameter names to the string values applied in this combination.</param>
public sealed record SweepCombination(
    GenerationParameters Parameters,
    int GridX,
    int GridY,
    Dictionary<string, string> AxisValues);

/// <summary>
/// Expands base <see cref="GenerationParameters"/> and one or two <see cref="SweepAxis"/> definitions
/// into a cartesian product of <see cref="SweepCombination"/> instances, each with a grid position.
/// </summary>
public static class SweepExpander
{
    // JsonSerializerOptions used for the round-trip clone.
    // Default options serialize enums as integers, which is what we need.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null, // PascalCase — matches C# property names
    };

    /// <summary>
    /// Expands the given base parameters using the supplied axes.
    /// </summary>
    /// <param name="baseParameters">The base set of generation parameters.</param>
    /// <param name="axis1">The primary sweep axis (columns). If null, returns a single combination.</param>
    /// <param name="axis2">The secondary sweep axis (rows). Optional.</param>
    /// <returns>An ordered list of all sweep combinations.</returns>
    public static IReadOnlyList<SweepCombination> Expand(
        GenerationParameters baseParameters,
        SweepAxis? axis1,
        SweepAxis? axis2 = null)
    {
        if (axis1 is null)
        {
            return
            [
                new SweepCombination(
                    baseParameters,
                    GridX: 0,
                    GridY: 0,
                    AxisValues: new Dictionary<string, string>())
            ];
        }

        var combinations = new List<SweepCombination>();

        if (axis2 is null)
        {
            for (var x = 0; x < axis1.Values.Count; x++)
            {
                var value1 = axis1.Values[x];
                var overridden = ApplyOverride(baseParameters, axis1.ParameterName, value1);
                var axisValues = new Dictionary<string, string>
                {
                    [axis1.ParameterName] = value1
                };
                combinations.Add(new SweepCombination(overridden, GridX: x, GridY: 0, axisValues));
            }
        }
        else
        {
            for (var y = 0; y < axis2.Values.Count; y++)
            {
                var value2 = axis2.Values[y];
                for (var x = 0; x < axis1.Values.Count; x++)
                {
                    var value1 = axis1.Values[x];
                    var afterAxis1 = ApplyOverride(baseParameters, axis1.ParameterName, value1);
                    var afterAxis2 = ApplyOverride(afterAxis1, axis2.ParameterName, value2);
                    var axisValues = new Dictionary<string, string>
                    {
                        [axis1.ParameterName] = value1,
                        [axis2.ParameterName] = value2
                    };
                    combinations.Add(new SweepCombination(afterAxis2, GridX: x, GridY: y, axisValues));
                }
            }
        }

        return combinations;
    }

    /// <summary>
    /// Clones <paramref name="parameters"/> and overrides the single named field with the supplied string value.
    /// </summary>
    /// <param name="parameters">The parameters to clone.</param>
    /// <param name="parameterName">The name of the property to override (must match a known field exactly).</param>
    /// <param name="value">The string representation of the new value.</param>
    /// <returns>A new <see cref="GenerationParameters"/> with the override applied.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="parameterName"/> is not a known parameter.</exception>
    public static GenerationParameters ApplyOverride(
        GenerationParameters parameters,
        string parameterName,
        string value)
    {
        // Serialize the current parameters to a JSON dictionary so we can patch a single key.
        var json = JsonSerializer.Serialize(parameters, SerializerOptions);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, SerializerOptions)
                   ?? throw new InvalidOperationException("Failed to deserialize GenerationParameters to dictionary.");

        // Validate the parameter name before mutating.
        if (!KnownParameters.Contains(parameterName))
            throw new ArgumentException($"Unknown parameter name: '{parameterName}'.", nameof(parameterName));

        dict[parameterName] = ConvertToJsonElement(parameterName, value);

        var patched = JsonSerializer.Serialize(dict, SerializerOptions);
        return JsonSerializer.Deserialize<GenerationParameters>(patched, SerializerOptions)
               ?? throw new InvalidOperationException("Failed to deserialize patched JSON back to GenerationParameters.");
    }

    /// <summary>
    /// Converts a raw string value for the named parameter into the appropriate <see cref="JsonElement"/>
    /// by parsing it according to the parameter's declared type.
    /// </summary>
    /// <param name="parameterName">The name of the target parameter field.</param>
    /// <param name="value">The string representation of the value.</param>
    /// <returns>A <see cref="JsonElement"/> suitable for inserting into the serialized parameter dictionary.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="parameterName"/> is unknown.</exception>
    public static JsonElement ConvertToJsonElement(string parameterName, string value)
    {
        return parameterName switch
        {
            // ── string fields ────────────────────────────────────────────────────────
            "PositivePrompt" or "NegativePrompt" or "InitImagePath" or "MaskImagePath"
                => JsonSerializer.SerializeToElement(value, SerializerOptions),

            // ── int fields ───────────────────────────────────────────────────────────
            "Steps" or "Width" or "Height" or "BatchSize" or "ClipSkip" or "BatchCount" or "HiresSteps"
                => JsonSerializer.SerializeToElement(int.Parse(value), SerializerOptions),

            // ── long field ───────────────────────────────────────────────────────────
            "Seed"
                => JsonSerializer.SerializeToElement(long.Parse(value), SerializerOptions),

            // ── double fields ────────────────────────────────────────────────────────
            "CfgScale" or "Eta" or "DenoisingStrength" or "HiresUpscaleFactor" or "HiresDenoisingStrength"
                => JsonSerializer.SerializeToElement(double.Parse(value, System.Globalization.CultureInfo.InvariantCulture), SerializerOptions),

            // ── bool field ───────────────────────────────────────────────────────────
            "HiresFixEnabled"
                => JsonSerializer.SerializeToElement(bool.Parse(value), SerializerOptions),

            // ── Guid fields ──────────────────────────────────────────────────────────
            "CheckpointModelId"
                => JsonSerializer.SerializeToElement(Guid.Parse(value), SerializerOptions),

            "VaeModelId"
                => JsonSerializer.SerializeToElement(
                    string.IsNullOrEmpty(value) ? (Guid?)null : Guid.Parse(value),
                    SerializerOptions),

            // ── enum fields (serialized as integer by default) ───────────────────────
            "Sampler"
                => JsonSerializer.SerializeToElement(
                    (int)Enum.Parse<Sampler>(value, ignoreCase: true),
                    SerializerOptions),

            "Scheduler"
                => JsonSerializer.SerializeToElement(
                    (int)Enum.Parse<Scheduler>(value, ignoreCase: true),
                    SerializerOptions),

            "Mode"
                => JsonSerializer.SerializeToElement(
                    (int)Enum.Parse<GenerationMode>(value, ignoreCase: true),
                    SerializerOptions),

            _ => throw new ArgumentException($"Unknown parameter name: '{parameterName}'.", nameof(parameterName))
        };
    }

    // Authoritative set of supported parameter names.  Used for early validation in ApplyOverride.
    private static readonly HashSet<string> KnownParameters = new(StringComparer.Ordinal)
    {
        "PositivePrompt", "NegativePrompt", "InitImagePath", "MaskImagePath",
        "Steps", "Width", "Height", "BatchSize", "ClipSkip", "BatchCount", "HiresSteps",
        "Seed",
        "CfgScale", "Eta", "DenoisingStrength", "HiresUpscaleFactor", "HiresDenoisingStrength",
        "HiresFixEnabled",
        "CheckpointModelId", "VaeModelId",
        "Sampler", "Scheduler", "Mode"
    };
}
