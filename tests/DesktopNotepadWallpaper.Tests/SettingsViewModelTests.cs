using DesktopNotepadWallpaper.Models;
using DesktopNotepadWallpaper.Services;
using DesktopNotepadWallpaper.Tests.Fakes;
using DesktopNotepadWallpaper.Tests.Helpers;
using DesktopNotepadWallpaper.ViewModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    private SettingsViewModel CreateViewModel(out DataService data, out AppConfig config)
    {
        data = new DataService(_temp.Path);
        config = data.LoadConfig();
        var gallery = new GalleryService(data.GalleryDir, config);
        var rotator = new WallpaperRotatorService(
            config, gallery, new FakeRenderer(), () => new List<TaskItem>(), new FakeWallpaperSetter());
        return new SettingsViewModel(gallery, data, config, rotator, new AutoStartService());
    }

    [Fact]
    public void ApplyBackgroundImage_转换并复制到Background目录_样式切换为模糊图片()
    {
        var vm = CreateViewModel(out var data, out var config);
        var source = _temp.GetFile("bg_source.png");
        using (var image = new Image<Rgba32>(32, 32, new Rgba32(10, 20, 30)))
        {
            image.Save(source);
        }

        vm.ApplyBackgroundImage(source);

        Assert.Equal("ImageBlur", config.WallpaperBackground.Style);
        Assert.True(vm.IsImageBlurStyle);
        Assert.False(vm.IsSolidStyle);
        Assert.False(vm.IsGradientStyle);
        Assert.NotNull(config.WallpaperBackground.ImagePath);
        Assert.StartsWith("Background", config.WallpaperBackground.ImagePath);
        var fullPath = Path.Combine(data.DataDir, config.WallpaperBackground.ImagePath);
        Assert.True(File.Exists(fullPath));
        Assert.Equal(Path.GetFileName(fullPath), vm.BgImageName);
        // 转换结果可被解码（PNG 有效）
        using var converted = Image.Load(fullPath);
        Assert.Equal(32, converted.Width);
    }

    [Fact]
    public void ApplyBackgroundImage_多次应用_文件不互相覆盖()
    {
        var vm = CreateViewModel(out var data, out var config);
        var source = _temp.GetFile("bg2.png");
        using (var image = new Image<Rgba32>(8, 8, new Rgba32(255, 0, 0)))
        {
            image.Save(source);
        }

        vm.ApplyBackgroundImage(source);
        var firstPath = Path.Combine(data.DataDir, config.WallpaperBackground.ImagePath);
        vm.ApplyBackgroundImage(source);
        var secondPath = Path.Combine(data.DataDir, config.WallpaperBackground.ImagePath);

        Assert.NotEqual(firstPath, secondPath);
        Assert.True(File.Exists(firstPath));
        Assert.True(File.Exists(secondPath));
    }
}
