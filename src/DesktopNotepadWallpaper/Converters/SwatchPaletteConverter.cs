using System.Globalization;
using System.Windows.Data;
using DesktopNotepadWallpaper.Models;

namespace DesktopNotepadWallpaper.Converters;

/// <summary>色板条目：绑定到任务对象，避免弹窗内逻辑树断裂导致的查找失败。</summary>
public sealed class HighlightSwatchEntry
{
    public HighlightSwatchEntry(string key, string display, System.Windows.Media.Color color, TaskItem task)
    {
        Key = key;
        Display = display;
        Color = color;
        Task = task;
    }

    public string Key { get; }

    public string Display { get; }

    public System.Windows.Media.Color Color { get; }

    public TaskItem Task { get; }
}

/// <summary>任务 → 携带任务引用的色板条目列表。</summary>
public sealed class SwatchPaletteConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TaskItem task)
        {
            return Array.Empty<HighlightSwatchEntry>();
        }
        return HighlightPalette.Items
            .Select(s => new HighlightSwatchEntry(s.Key, s.Display, s.Color, task))
            .ToArray();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
