namespace StableDiffusionStudio.Domain.Entities;

public class Setting
{
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }

    private Setting() { } // EF Core

    public static Setting Create(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key is required.", nameof(key));

        return new Setting
        {
            Key = key,
            Value = value,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(string value)
    {
        Value = value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
