using MudBlazor;

namespace StableDiffusionStudio.Web.Theme;

public static class StudioTheme
{
    public static MudTheme Create() => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#58a6ff",
            Secondary = "#bc8cff",
            AppbarBackground = "#161b22",
            Background = "#0d1117",
            Surface = "#161b22",
            DrawerBackground = "#0d1117",
            TextPrimary = "#e6edf3",
            TextSecondary = "#8b949e",
            ActionDefault = "#8b949e",
            Success = "#3fb950",
            Warning = "#d29922",
            Error = "#f85149",
            Info = "#58a6ff"
        },
        LayoutProperties = new LayoutProperties
        {
            DrawerWidthLeft = "260px"
        }
    };
}
