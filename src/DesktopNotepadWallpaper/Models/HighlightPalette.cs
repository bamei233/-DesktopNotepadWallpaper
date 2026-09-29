using System.Windows.Media;

namespace DesktopNotepadWallpaper.Models;

public sealed record HighlightSwatch(string Key, string Display, Color Color);

public static class HighlightPalette
{
    public const string None = "None";

    public static readonly IReadOnlyList<HighlightSwatch> Items = new[]
    {
        new HighlightSwatch(None,     "无", Colors.Transparent),
        new HighlightSwatch("Yellow", "黄", Color.FromRgb(255, 214, 102)),
        new HighlightSwatch("Orange", "橙", Color.FromRgb(255, 167, 88)),
        new HighlightSwatch("Green",  "绿", Color.FromRgb(129, 199, 132)),
        new HighlightSwatch("Blue",   "蓝", Color.FromRgb(129, 183, 240)),
        new HighlightSwatch("Purple", "紫", Color.FromRgb(186, 148, 240)),
        new HighlightSwatch("Pink",   "粉", Color.FromRgb(240, 148, 178)),
        new HighlightSwatch("Red",    "红", Color.FromRgb(239, 138, 132)),
    };

    public static Color GetColor(string key)
    {
        foreach (var item in Items)
        {
            if (item.Key == key)
            {
                return item.Color;
            }
        }
        return Colors.Transparent;
    }
}
