namespace StableDiffusionStudio.Domain.ValueObjects;

public sealed record SweepAxis
{
    public required string ParameterName { get; init; }
    public required IReadOnlyList<string> Values { get; init; }
    public string? Label { get; init; }
    public string DisplayLabel => Label ?? ParameterName;

    public static SweepAxis Numeric(string parameterName, double start, double end, double step, string? label = null)
    {
        if (step <= 0) throw new ArgumentException("Step must be positive.", nameof(step));
        if (start > end) throw new ArgumentException("Start must be <= end.", nameof(start));
        var values = new List<string>();
        for (var v = start; v <= end + step / 100.0; v += step)
            values.Add(Math.Round(v, 10).ToString("G"));
        return new SweepAxis { ParameterName = parameterName, Values = values, Label = label };
    }

    public static SweepAxis Categorical(string parameterName, IEnumerable<string> values, string? label = null)
    {
        var list = values.ToList();
        if (list.Count == 0) throw new ArgumentException("At least one value is required.", nameof(values));
        return new SweepAxis { ParameterName = parameterName, Values = list, Label = label };
    }
}
