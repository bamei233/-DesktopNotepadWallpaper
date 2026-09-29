using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DesktopNotepadWallpaper.Models;

namespace DesktopNotepadWallpaper.Converters;

/// <summary>高亮色 key → 半透明行背景画刷；无高亮返回透明。</summary>
public sealed class RowHighlightConverter : IValueConverter
{
    private const byte RowAlpha = 40;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = HighlightPalette.GetColor(value as string ?? HighlightPalette.None);
        if (color == Colors.Transparent)
        {
            return Brushes.Transparent;
        }
        return new SolidColorBrush(Color.FromArgb(RowAlpha, color.R, color.G, color.B));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
