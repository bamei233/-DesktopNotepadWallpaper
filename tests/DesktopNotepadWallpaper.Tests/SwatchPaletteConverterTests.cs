using DesktopNotepadWallpaper.Converters;
using DesktopNotepadWallpaper.Models;
using System.Globalization;
using System.Text.Json;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

public sealed class SwatchPaletteConverterTests
{
    [Fact]
    public void Convert_任务转为携带任务引用的色板条目()
    {
        var task = new TaskItem { Description = "测试" };
        var converter = new SwatchPaletteConverter();

        var entries = Assert.IsType<HighlightSwatchEntry[]>(
            converter.Convert(task, typeof(object), null, CultureInfo.InvariantCulture));

        Assert.Equal(HighlightPalette.Items.Count, entries.Length);
        Assert.All(entries, e => Assert.Same(task, e.Task));
        var blue = entries.First(e => e.Key == "Blue");
        Assert.Equal(HighlightPalette.GetColor("Blue"), blue.Color);
        Assert.Equal("蓝", blue.Display);
    }

    [Fact]
    public void Convert_非任务输入_返回空数组()
    {
        var converter = new SwatchPaletteConverter();

        var entries = Assert.IsType<HighlightSwatchEntry[]>(
            converter.Convert("不是任务", typeof(object), null, CultureInfo.InvariantCulture));

        Assert.Empty(entries);
    }

    [Fact]
    public void IsPaletteOpen_不参与JSON序列化()
    {
        var task = new TaskItem { IsPaletteOpen = true };

        var json = JsonSerializer.Serialize(task);

        Assert.DoesNotContain("IsPaletteOpen", json);
    }
}
