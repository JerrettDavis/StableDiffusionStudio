using MudBlazor;

namespace StableDiffusionStudio.Web.Theme;

public static class StudioTheme
{
    public static MudTheme Create() => new()
    {
        PaletteDark = new PaletteDark
        {
            // Warm industrial dark palette
            Primary = "#e8a849",
            Secondary = "#7c9cb8",
            Tertiary = "#b87c5c",
            Background = "#12131a",
            Surface = "#1a1b24",
            DrawerBackground = "#14151e",
            AppbarBackground = "#16171f",
            TextPrimary = "#e8e4df",
            TextSecondary = "#8b8680",
            TextDisabled = "#4a4743",
            ActionDefault = "#8b8680",
            ActionDisabled = "#3a3835",
            Success = "#6dbf73",
            Warning = "#e8a849",
            Error = "#d65c5c",
            Info = "#7c9cb8",
            Divider = "#2a2b35",
            LinesDefault = "#2a2b35",
            TableLines = "#2a2b35",
            OverlayDark = "rgba(0,0,0,0.65)",
        },
        PaletteLight = new PaletteLight
        {
            Primary = "#c48a2a",
            Secondary = "#5a7a94",
            Tertiary = "#8b5a3c",
            Background = "#f4f0eb",
            Surface = "#faf7f3",
            DrawerBackground = "#f0ece6",
            AppbarBackground = "#faf7f3",
            TextPrimary = "#1a1815",
            TextSecondary = "#6b6560",
            Success = "#4a8f50",
            Warning = "#c48a2a",
            Error = "#b84040",
            Info = "#5a7a94",
            Divider = "#ddd8d0",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Inter", "system-ui", "-apple-system", "sans-serif" },
                FontSize = "0.875rem",
                LineHeight = "1.5",
            },
            H4 = new H4Typography
            {
                FontFamily = new[] { "Inter", "system-ui", "sans-serif" },
                FontWeight = "700",
                FontSize = "1.5rem",
                LetterSpacing = "-0.02em",
            },
            H5 = new H5Typography
            {
                FontWeight = "600",
                FontSize = "1.25rem",
                LetterSpacing = "-0.01em",
            },
            H6 = new H6Typography
            {
                FontWeight = "600",
                FontSize = "1rem",
                LetterSpacing = "-0.01em",
            },
            Subtitle1 = new Subtitle1Typography
            {
                FontWeight = "500",
                FontSize = "0.9375rem",
            },
            Subtitle2 = new Subtitle2Typography
            {
                FontWeight = "600",
                FontSize = "0.8125rem",
                LetterSpacing = "0.04em",
                TextTransform = "uppercase",
            },
            Caption = new CaptionTypography
            {
                FontFamily = new[] { "JetBrains Mono", "Consolas", "monospace" },
                FontSize = "0.75rem",
            },
        },
        LayoutProperties = new LayoutProperties
        {
            DrawerWidthLeft = "240px",
            DefaultBorderRadius = "6px",
        },
    };
}
