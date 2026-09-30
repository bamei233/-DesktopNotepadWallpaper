using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DesktopNotepadWallpaper.Models;

namespace DesktopNotepadWallpaper.Converters;

/// <summary>高亮色 key → 色点画刷；无高亮时返回浅灰底（浅色主题下可见）。</summary>
public sealed class HighlightKeyBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush NoneBrush = new(Color.FromRgb(0xEF, 0xEF, 0xF3));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = HighlightPalette.GetColor(value as string ?? HighlightPalette.None);
        if (color == Colors.Transparent)
        {
            return NoneBrush;
        }
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
