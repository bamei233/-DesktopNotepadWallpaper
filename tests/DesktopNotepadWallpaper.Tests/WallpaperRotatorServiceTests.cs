using DesktopNotepadWallpaper.Models;
using DesktopNotepadWallpaper.Services;
using DesktopNotepadWallpaper.Tests.Fakes;
using DesktopNotepadWallpaper.Tests.Helpers;
using System.IO;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

public sealed class WallpaperRotatorServiceTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly AppConfig _config = new();
    private readonly GalleryService _gallery;
    private readonly List<TaskItem> _tasks = new() { new TaskItem { Description = "任务" } };
    private readonly FakeRenderer _renderer = new();
    private readonly FakeWallpaperSetter _setter = new();

    public WallpaperRotatorServiceTests()
    {
        _gallery = new GalleryService(_temp.GetFile("Gallery"), _config);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }

    private WallpaperRotatorService CreateRotator()
    {
        return new WallpaperRotatorService(_config, _gallery, _renderer, () => _tasks, _setter);
    }

    private void FillGallery(int count)
    {
        _config.GalleryImages.Clear();
        for (var i = 1; i <= count; i++)
        {
            var name = $"img_{i}.png";
            _config.GalleryImages.Add(name);
            File.WriteAllText(Path.Combine(_gallery.GalleryDir, name), "dummy");
        }
    }

    [Fact]
    public void Initialize_索引为0_渲染并应用记事本壁纸()
    {
        var rotator = CreateRotator();

        rotator.Initialize();

        Assert.Equal(0, rotator.CurrentIndex);
        Assert.Equal(1, _renderer.RenderCallCount);
        Assert.Single(_setter.AppliedPaths);
        Assert.Equal(_renderer.CacheFilePath, _setter.AppliedPaths[0]);
    }

    [Fact]
    public void Next_从记事本到第1张_循环回到第1张()
    {
        FillGallery(3);
        var rotator = CreateRotator();
        rotator.Initialize();
        _setter.AppliedPaths.Clear();

        rotator.Next();
        Assert.Equal(1, rotator.CurrentIndex);
        rotator.Next();
        Assert.Equal(2, rotator.CurrentIndex);
        rotator.Next();
        Assert.Equal(3, rotator.CurrentIndex);
        rotator.Next();
        Assert.Equal(1, rotator.CurrentIndex);

        Assert.Equal(4, _setter.AppliedPaths.Count);
        Assert.Equal(Path.Combine(_gallery.GalleryDir, "img_1.png"), _setter.AppliedPaths[0]);
        Assert.Equal(Path.Combine(_gallery.GalleryDir, "img_3.png"), _setter.AppliedPaths[2]);
    }

    [Fact]
    public void Previous_从第1张到最后一张_从记事本到最后一张()
    {
        FillGallery(3);
        var rotator = CreateRotator();
        rotator.Initialize();
        _setter.AppliedPaths.Clear();

        rotator.Next();
        Assert.Equal(1, rotator.CurrentIndex);
        rotator.Previous();
        Assert.Equal(3, rotator.CurrentIndex);
        rotator.Previous();
        Assert.Equal(2, rotator.CurrentIndex);
        rotator.Previous();
        Assert.Equal(1, rotator.CurrentIndex);
    }

    [Fact]
    public void ShowNotepad_切回记事本_始终渲染最新内容()
    {
        FillGallery(3);
        var rotator = CreateRotator();
        rotator.Initialize();
        rotator.Next();
        rotator.Next();
        _setter.AppliedPaths.Clear();

        rotator.ShowNotepad();

        Assert.Equal(0, rotator.CurrentIndex);
        Assert.Single(_setter.AppliedPaths);
        Assert.Equal(_renderer.CacheFilePath, _setter.AppliedPaths[0]);
    }

    [Fact]
    public void 图库为空_切换操作落在记事本上()
    {
        var rotator = CreateRotator();
        rotator.Initialize();
        _setter.AppliedPaths.Clear();

        rotator.Next();
        rotator.Previous();

        Assert.Equal(0, rotator.CurrentIndex);
        Assert.Equal(2, _setter.AppliedPaths.Count);
        Assert.All(_setter.AppliedPaths, p => Assert.Equal(_renderer.CacheFilePath, p));
    }

    [Fact]
    public void OnGalleryChanged_图库清空_回到记事本()
    {
        FillGallery(3);
        var rotator = CreateRotator();
        rotator.Initialize();
        rotator.Next();
        rotator.Next();
        rotator.Next();
        Assert.Equal(3, rotator.CurrentIndex);
        _setter.AppliedPaths.Clear();

        _config.GalleryImages.Clear();
        rotator.OnGalleryChanged();

        Assert.Equal(0, rotator.CurrentIndex);
        Assert.Equal(_renderer.CacheFilePath, _setter.AppliedPaths[0]);
    }

    [Fact]
    public void OnGalleryChanged_删除当前之后的图_索引被修正到末尾()
    {
        FillGallery(5);
        var rotator = CreateRotator();
        rotator.Initialize();
        rotator.Next();
        rotator.Next();
        rotator.Next();
        rotator.Next();
        rotator.Next();
        Assert.Equal(5, rotator.CurrentIndex);
        _setter.AppliedPaths.Clear();

        _config.GalleryImages.RemoveAt(4);
        _config.GalleryImages.RemoveAt(3);
        rotator.OnGalleryChanged();

        Assert.Equal(3, rotator.CurrentIndex);
        Assert.Equal(Path.Combine(_gallery.GalleryDir, "img_3.png"), _setter.AppliedPaths[0]);
    }

    [Fact]
    public void OnGalleryChanged_当前图片文件丢失_回退记事本()
    {
        FillGallery(2);
        var rotator = CreateRotator();
        rotator.Initialize();
        rotator.Next();
        Assert.Equal(1, rotator.CurrentIndex);
        File.Delete(Path.Combine(_gallery.GalleryDir, "img_1.png"));
        _setter.AppliedPaths.Clear();

        rotator.OnGalleryChanged();

        Assert.Equal(0, rotator.CurrentIndex);
        Assert.Equal(_renderer.CacheFilePath, _setter.AppliedPaths[0]);
    }

    [Fact]
    public void RefreshNotepadWallpaper_图库模式只更新缓存不应用_记事本模式直接应用()
    {
        FillGallery(2);
        var rotator = CreateRotator();
        rotator.Initialize();
        var renderedAtInitialize = _renderer.RenderCallCount;

        rotator.RefreshNotepadWallpaper();
        Assert.Equal(renderedAtInitialize + 1, _renderer.RenderCallCount);
        Assert.Equal(_renderer.CacheFilePath, _setter.AppliedPaths[^1]);

        rotator.Next();
        _setter.AppliedPaths.Clear();

        rotator.RefreshNotepadWallpaper();
        Assert.Equal(renderedAtInitialize + 2, _renderer.RenderCallCount);
        Assert.Empty(_setter.AppliedPaths);
    }

    [Fact]
    public void 渲染失败_不应用壁纸()
    {
        var failingRenderer = new FailingRenderer();
        var rotator = new WallpaperRotatorService(
            _config, _gallery, failingRenderer, () => _tasks, _setter);

        rotator.Initialize();

        Assert.Empty(_setter.AppliedPaths);
        Assert.Equal(0, rotator.CurrentIndex);
    }
}
