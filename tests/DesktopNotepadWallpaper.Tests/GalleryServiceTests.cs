using DesktopNotepadWallpaper.Models;
using DesktopNotepadWallpaper.Services;
using DesktopNotepadWallpaper.Tests.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

public sealed class GalleryServiceTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly AppConfig _config = new();
    private readonly GalleryService _service;

    public GalleryServiceTests()
    {
        _service = new GalleryService(_temp.GetFile("Gallery"), _config);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }

    private string CreatePng(string name)
    {
        var path = _temp.GetFile(name);
        using var image = new Image<Rgba32>(16, 16, new Rgba32(100, 150, 200));
        image.Save(path);
        return path;
    }

    private string CreateWebp(string name)
    {
        var path = _temp.GetFile(name);
        using var image = new Image<Rgba32>(16, 16, new Rgba32(200, 100, 50));
        image.Save(path, new WebpEncoder());
        return path;
    }

    [Fact]
    public void Import_png图片_复制进图库并登记()
    {
        var source = CreatePng("source.png");

        var (imported, rejected) = _service.ImportFiles(new[] { source });

        Assert.Equal(1, imported);
        Assert.Equal(0, rejected);
        Assert.Single(_config.GalleryImages);
        var name = _config.GalleryImages[0];
        Assert.EndsWith(".png", name);
        Assert.True(File.Exists(Path.Combine(_service.GalleryDir, name)));
        // 原文件不动
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void Import_webp图片_转换为png()
    {
        var source = CreateWebp("source.webp");

        var (imported, _) = _service.ImportFiles(new[] { source });

        Assert.Equal(1, imported);
        var galleryFile = Path.Combine(_service.GalleryDir, _config.GalleryImages[0]);
        using var image = Image.Load(galleryFile);
        Assert.Equal(16, image.Width);
        Assert.Equal(16, image.Height);
    }

    [Fact]
    public void Import_不支持的格式_拒绝()
    {
        var textFile = _temp.GetFile("notes.txt");
        File.WriteAllText(textFile, "not an image");

        var (imported, rejected) = _service.ImportFiles(new[] { textFile });

        Assert.Equal(0, imported);
        Assert.Equal(1, rejected);
        Assert.Empty(_config.GalleryImages);
    }

    [Fact]
    public void Import_不存在的文件_拒绝()
    {
        var (imported, rejected) = _service.ImportFiles(new[] { _temp.GetFile("ghost.png") });

        Assert.Equal(0, imported);
        Assert.Equal(1, rejected);
    }

    [Fact]
    public void Import_超过20张上限_拒绝超出的部分()
    {
        for (var i = 0; i < GalleryService.MaxImages; i++)
        {
            var (imported, _) = _service.ImportFiles(new[] { CreatePng($"fill_{i}.png") });
            Assert.Equal(1, imported);
        }
        Assert.Equal(GalleryService.MaxImages, _config.GalleryImages.Count);

        var (extraImported, extraRejected) = _service.ImportFiles(new[]
        {
            CreatePng("extra1.png"),
            CreatePng("extra2.png")
        });

        Assert.Equal(0, extraImported);
        Assert.Equal(2, extraRejected);
        Assert.Equal(GalleryService.MaxImages, _config.GalleryImages.Count);
    }

    [Fact]
    public void Delete_删除文件并移除登记()
    {
        _service.ImportFiles(new[] { CreatePng("to_delete.png") });
        var name = _config.GalleryImages[0];
        var filePath = Path.Combine(_service.GalleryDir, name);
        Assert.True(File.Exists(filePath));

        _service.Delete(name);

        Assert.Empty(_config.GalleryImages);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void Reorder_按给定顺序重排()
    {
        _service.ImportFiles(new[]
        {
            CreatePng("r1.png"), CreatePng("r2.png"), CreatePng("r3.png")
        });
        var names = _config.GalleryImages.ToList();

        _service.Reorder(new[] { names[2], names[0], names[1] });

        Assert.Equal(new[] { names[2], names[0], names[1] }, _config.GalleryImages);
    }

    [Fact]
    public void ClearAll_清空文件与登记()
    {
        _service.ImportFiles(new[]
        {
            CreatePng("c1.png"), CreatePng("c2.png")
        });
        var files = _config.GalleryImages
            .Select(n => Path.Combine(_service.GalleryDir, n))
            .ToList();

        _service.ClearAll();

        Assert.Empty(_config.GalleryImages);
        Assert.All(files, f => Assert.False(File.Exists(f)));
    }
}
