using DesktopNotepadWallpaper.Models;
using DesktopNotepadWallpaper.Services;
using DesktopNotepadWallpaper.Tests.Helpers;
using System.IO;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

public sealed class DataServiceTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public void 构造函数_自动创建Data_Gallery_Cache目录()
    {
        var service = new DataService(_temp.Path);

        Assert.True(Directory.Exists(service.DataDir));
        Assert.True(Directory.Exists(service.GalleryDir));
        Assert.True(Directory.Exists(service.CacheDir));
        Assert.Equal(_temp.Path, service.DataDir);
    }

    [Fact]
    public void SaveLoad_任务含中文与特殊字符_完整往返()
    {
        var service = new DataService(_temp.Path);
        var original = new List<TaskItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Description = "完成《记事壁纸》开发 · 进度 100%",
                IsCompleted = true,
                Deadline = new DateTime(2026, 10, 1, 9, 30, 0),
                Tags = new List<string> { "开发", "重点" },
                HighlightColor = "Blue",
                SortIndex = 7
            },
            new()
            {
                Description = "空任务默认值",
                SortIndex = 0
            }
        };

        service.SaveTasks(original);
        var loaded = service.LoadTasks();

        Assert.Equal(2, loaded.Count);
        var first = loaded[0];
        Assert.Equal(original[0].Id, first.Id);
        Assert.Equal("完成《记事壁纸》开发 · 进度 100%", first.Description);
        Assert.True(first.IsCompleted);
        Assert.Equal(new DateTime(2026, 10, 1, 9, 30, 0), first.Deadline);
        Assert.Equal(new[] { "开发", "重点" }, first.Tags);
        Assert.Equal("Blue", first.HighlightColor);
        Assert.Equal(7, first.SortIndex);
        var second = loaded[1];
        Assert.False(second.IsCompleted);
        Assert.Null(second.Deadline);
        Assert.Empty(second.Tags);
        Assert.Equal(HighlightPalette.None, second.HighlightColor);
    }

    [Fact]
    public void SaveConfig_图库列表与字体设置_往返一致()
    {
        var service = new DataService(_temp.Path);
        var config = new AppConfig
        {
            AutoStart = false,
            MinimizeToTray = false,
            GalleryImages = new List<string> { "a.png", "b.png" },
        };
        config.WallpaperBackground.Style = "ImageBlur";
        config.WallpaperBackground.BlurRadius = 42;
        config.Font.Family = "KaiTi";
        config.Font.BaseSize = 26;
        config.Font.Color = "#FFEE00";
        config.Padding.Left = 80;
        config.Padding.Top = 64;
        config.Padding.Right = 96;
        config.Padding.Bottom = 48;

        service.SaveConfig(config);
        var loaded = service.LoadConfig();

        Assert.False(loaded.AutoStart);
        Assert.False(loaded.MinimizeToTray);
        Assert.Equal(new[] { "a.png", "b.png" }, loaded.GalleryImages);
        Assert.Equal("ImageBlur", loaded.WallpaperBackground.Style);
        Assert.Equal(42, loaded.WallpaperBackground.BlurRadius);
        Assert.Equal("KaiTi", loaded.Font.Family);
        Assert.Equal(26, loaded.Font.BaseSize);
        Assert.Equal("#FFEE00", loaded.Font.Color);
        Assert.Equal(80, loaded.Padding.Left);
        Assert.Equal(64, loaded.Padding.Top);
        Assert.Equal(96, loaded.Padding.Right);
        Assert.Equal(48, loaded.Padding.Bottom);
    }

    [Fact]
    public void 默认配置_字号为22_内边距为56()
    {
        var service = new DataService(_temp.Path);

        var config = service.LoadConfig();

        Assert.Equal(22, config.Font.BaseSize);
        Assert.Equal(56, config.Padding.Left);
        Assert.Equal(56, config.Padding.Top);
        Assert.Equal(56, config.Padding.Right);
        Assert.Equal(56, config.Padding.Bottom);
    }

    [Fact]
    public void Load_文件不存在_返回默认值而非抛异常()
    {
        var service = new DataService(_temp.Path);

        Assert.Empty(service.LoadTasks());
        var config = service.LoadConfig();
        Assert.NotNull(config);
        Assert.True(config.AutoStart);
        Assert.True(config.MinimizeToTray);
        Assert.Equal("Gradient", config.WallpaperBackground.Style);
    }

    [Fact]
    public void Load_JSON损坏_返回默认值而非抛异常()
    {
        var service = new DataService(_temp.Path);
        File.WriteAllText(service.TasksFilePath, "{ 这不是合法 JSON [");
        File.WriteAllText(service.ConfigFilePath, "彻底损坏的内容");

        Assert.Empty(service.LoadTasks());
        Assert.NotNull(service.LoadConfig());
    }

    [Fact]
    public void Save_写临时文件_最终无残留tmp()
    {
        var service = new DataService(_temp.Path);
        service.SaveTasks(new List<TaskItem> { new() { Description = "临时文件测试" } });

        Assert.False(File.Exists(service.TasksFilePath + ".tmp"));
        Assert.True(File.Exists(service.TasksFilePath));
    }
}
