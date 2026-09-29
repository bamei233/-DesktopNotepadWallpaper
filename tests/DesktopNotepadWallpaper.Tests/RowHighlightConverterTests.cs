using DesktopNotepadWallpaper.Converters;
using DesktopNotepadWallpaper.Models;
using System.Globalization;
using System.Windows.Media;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

public sealed class RowHighlightConverterTests
{
    private readonly RowHighlightConverter _converter = new();

    [Fact]
    public void None_返回透明画刷()
    {
        var result = (SolidColorBrush)_converter.Convert(HighlightPalette.None, typeof(Brush), null, CultureInfo.InvariantCulture);

        Assert.Equal(Colors.Transparent, result.Color);
    }

    [Fact]
    public void 无效key_返回透明画刷()
    {
        var result = (SolidColorBrush)_converter.Convert("NotAKey", typeof(Brush), null, CultureInfo.InvariantCulture);

        Assert.Equal(Colors.Transparent, result.Color);
    }

    [Theory]
    [InlineData("Blue", 129, 183, 240)]
    [InlineData("Red", 239, 138, 132)]
    public void 有效颜色_返回低透明度背景(string key, byte r, byte g, byte b)
    {
        var result = (SolidColorBrush)_converter.Convert(key, typeof(Brush), null, CultureInfo.InvariantCulture);

        Assert.Equal(Color.FromArgb(40, r, g, b), result.Color);
    }
}
