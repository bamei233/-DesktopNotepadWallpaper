using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DesktopNotepadWallpaper.Models;

namespace DesktopNotepadWallpaper.Converters;

public sealed class HighlightKeyBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = HighlightPalette.GetColor(value as string ?? HighlightPalette.None);
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
