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
}
