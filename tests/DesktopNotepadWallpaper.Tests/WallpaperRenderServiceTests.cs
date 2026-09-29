using DesktopNotepadWallpaper.Models;
using DesktopNotepadWallpaper.Services;
using DesktopNotepadWallpaper.Tests.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

internal static class Win32Screen
{
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public static int Width => GetSystemMetrics(SM_CXSCREEN);

    public static int Height => GetSystemMetrics(SM_CYSCREEN);
}

public sealed class WallpaperRenderServiceTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    private static List<TaskItem> SampleTasks(int count)
    {
        var tasks = new List<TaskItem>();
        for (var i = 0; i < count; i++)
        {
            tasks.Add(new TaskItem
            {
                Description = $"任务 {i + 1}：这是一条用于测试壁纸渲染的中文描述，包含一些较长的文字内容",
                IsCompleted = i % 3 == 0,
                Deadline = i % 2 == 0 ? DateTime.Now.AddDays(i) : null,
                Tags = i % 2 == 0 ? new List<string> { "标签A", "标签B" } : new List<string>(),
                HighlightColor = HighlightPalette.Items[(i % HighlightPalette.Items.Count)].Key,
                SortIndex = i
            });
        }
        return tasks;
    }

    private string CreateBackgroundImage()
    {
        var path = _temp.GetFile("bg.png");
        using (var image = new Image<Rgba32>(800, 600, new Rgba32(30, 60, 90)))
        {
            image.Save(path);
        }
        return path;
    }

    [Theory]
    [InlineData("Solid")]
    [InlineData("Gradient")]
    [InlineData("ImageBlur")]
    public void Render_三种背景样式_输出与主屏分辨率一致的PNG(string style)
    {
        var cacheDir = _temp.GetFile("Cache");
        var service = new WallpaperRenderService(cacheDir);
        var config = new AppConfig();
        config.WallpaperBackground.Style = style;
        if (style == "ImageBlur")
        {
            config.WallpaperBackground.ImagePath = "bg.png";
            CreateBackgroundImage();
        }

        (bool Succeeded, string? Error) result = (false, null);
        StaHelper.Run(() => result = service.Render(SampleTasks(3), config));

        Assert.True(result.Succeeded, result.Error);
        Assert.True(File.Exists(service.CacheFilePath));
        using var image = Image.Load(service.CacheFilePath);
        Assert.Equal(Win32Screen.Width, image.Width);
        Assert.Equal(Win32Screen.Height, image.Height);
    }

    [Fact]
    public void Render_空任务列表_正常输出背景图()
    {
        var cacheDir = _temp.GetFile("Cache");
        var service = new WallpaperRenderService(cacheDir);

        (bool Succeeded, string? Error) result = (false, null);
        StaHelper.Run(() => result = service.Render(new List<TaskItem>(), new AppConfig()));

        Assert.True(result.Succeeded, result.Error);
        Assert.True(File.Exists(service.CacheFilePath));
    }

    [Fact]
    public void Render_内容超过一屏_自动缩小字号仍渲染成功()
    {
        var cacheDir = _temp.GetFile("Cache");
        var service = new WallpaperRenderService(cacheDir);
        var config = new AppConfig();

        (bool Succeeded, string? Error) result = (false, null);
        StaHelper.Run(() => result = service.Render(SampleTasks(120), config));

        Assert.True(result.Succeeded, result.Error);
        Assert.True(File.Exists(service.CacheFilePath));
        Assert.True(new FileInfo(service.CacheFilePath).Length > 0);
    }

    [Fact]
    public void Render_模糊图片样式但图片不存在_回退深色背景仍成功()
    {
        var cacheDir = _temp.GetFile("Cache");
        var service = new WallpaperRenderService(cacheDir);
        var config = new AppConfig();
        config.WallpaperBackground.Style = "ImageBlur";
        config.WallpaperBackground.ImagePath = "missing.png";

        (bool Succeeded, string? Error) result = (false, null);
        StaHelper.Run(() => result = service.Render(SampleTasks(2), config));

        Assert.True(result.Succeeded, result.Error);
        Assert.True(File.Exists(service.CacheFilePath));
    }

    [Fact]
    public void Render_字体颜色非法值_回退默认仍成功()
    {
        var cacheDir = _temp.GetFile("Cache");
        var service = new WallpaperRenderService(cacheDir);
        var config = new AppConfig();
        config.Font.Color = "not-a-color";

        (bool Succeeded, string? Error) result = (false, null);
        StaHelper.Run(() => result = service.Render(SampleTasks(1), config));

        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public void Render_模糊图片_竖图高DPI_完整图片可见而非只显示局部()
    {
        var cacheDir = _temp.GetFile("Cache");
        var service = new WallpaperRenderService(cacheDir);
        var config = new AppConfig();
        config.WallpaperBackground.Style = "ImageBlur";
        config.WallpaperBackground.ImagePath = "bands.png";

        // 竖图 100×300 @300DPI，上中下三段：红/绿/蓝
        var sourcePath = _temp.GetFile("bands.png");
        using (var source = new Image<Rgba32>(100, 300))
        {
            for (var y = 0; y < 300; y++)
            {
                for (var x = 0; x < 100; x++)
                {
                    source[x, y] = y < 100
                        ? new Rgba32(255, 0, 0)
                        : y < 200 ? new Rgba32(0, 180, 0) : new Rgba32(0, 0, 220);
                }
            }
            source.Metadata.HorizontalResolution = 300;
            source.Metadata.VerticalResolution = 300;
            source.Metadata.ResolutionUnits = SixLabors.ImageSharp.Metadata.PixelResolutionUnit.PixelsPerInch;
            source.Save(sourcePath);
        }

        (bool Succeeded, string? Error) result = (false, null);
        StaHelper.Run(() => result = service.Render(SampleTasks(1), config));

        Assert.True(result.Succeeded, result.Error);

        using var output = Image.Load<Rgba32>(service.CacheFilePath);
        var cx = output.Width / 2;
        // contain 模式：竖图整图显示，三段颜色都应出现在屏幕中部竖线上
        // （深色遮罩后颜色约为原值的 56.9%）
        void AssertBand(int y, string name, Func<Rgba32, bool> predicate)
        {
            var pixel = output[cx, y];
            Assert.True(predicate(pixel),
                $"{name} 色带应可见于 (x={cx}, y={y})，实际像素 R={pixel.R} G={pixel.G} B={pixel.B}");
        }

        AssertBand((int)(output.Height * 0.20), "红", p => p.R > 110 && p.G < 70 && p.B < 70);
        AssertBand((int)(output.Height * 0.50), "绿", p => p.G > 90 && p.R < 70 && p.B < 70);
        AssertBand((int)(output.Height * 0.80), "蓝", p => p.B > 110 && p.R < 70 && p.G < 70);
    }
}
