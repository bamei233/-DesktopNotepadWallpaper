namespace DesktopNotepadWallpaper.Models;

public sealed class AppConfig
{
    public bool AutoStart { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public List<string> GalleryImages { get; set; } = new();
    public WallpaperPaddingConfig Padding { get; set; } = new();
    public BackgroundStyleConfig WallpaperBackground { get; set; } = new();
    public FontStyleConfig Font { get; set; } = new();
}

/// <summary>记事本壁纸内容区四边内边距（DIP）。</summary>
public sealed class WallpaperPaddingConfig
{
    public double Left { get; set; } = 56;
    public double Top { get; set; } = 56;
    public double Right { get; set; } = 56;
    public double Bottom { get; set; } = 56;
}

public sealed class BackgroundStyleConfig
{
    public string Style { get; set; } = "Gradient";
    public string SolidColor { get; set; } = "#2E3440";
    public string GradientStart { get; set; } = "#355C7D";
    public string GradientEnd { get; set; } = "#6C5B7B";
    public string ImagePath { get; set; } = string.Empty;
    public double BlurRadius { get; set; } = 30;
}

public sealed class FontStyleConfig
{
    public string Family { get; set; } = "Microsoft YaHei UI";
    public double BaseSize { get; set; } = 22;
    public string Color { get; set; } = "#FFFFFF";
}
