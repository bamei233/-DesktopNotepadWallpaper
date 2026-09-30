using DesktopNotepadWallpaper.Helpers;
using DesktopNotepadWallpaper.Tests.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using System.Windows.Media.Imaging;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

[Collection("UI")]
public sealed class ThumbnailCacheTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly StaUiFixture _sta;

    public ThumbnailCacheTests(StaUiFixture sta)
    {
        _sta = sta;
    }

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public void LoadAsync_加载图片_回调得到缩略图并命中缓存()
    {
        var path = _temp.GetFile("photo.png");
        using (var image = new Image<Rgba32>(640, 400))
        {
            for (var x = 0; x < 640; x++)
            {
                for (var y = 0; y < 400; y++)
                {
                    image[x, y] = new Rgba32((byte)(x % 255), (byte)(y % 255), 128);
                }
            }
            image.Save(path);
        }

        using var done = new ManualResetEventSlim();
        BitmapSource? loaded = null;
        // 在 fixture 线程发起加载；等待放在外面，避免阻塞 fixture 调度器
        _sta.Run(() =>
        {
            ThumbnailCache.LoadAsync(path, 320, _sta.Dispatcher, bitmap =>
            {
                loaded = bitmap;
                done.Set();
            });
        });

        Assert.True(done.Wait(TimeSpan.FromSeconds(15)), "缩略图加载未在超时时间内完成");
        Assert.NotNull(loaded);
        Assert.Equal(320, loaded!.PixelWidth);
        Assert.True(loaded.IsFrozen, "跨线程使用的位图必须已冻结");

        // 第二次加载应命中缓存并立即回调
        var cached = ThumbnailCache.TryGet(path, out var fromCache);
        Assert.True(cached);
        Assert.Same(loaded, fromCache);
    }

    [Fact]
    public void LoadAsync_文件不存在_不回调不崩溃()
    {
        _sta.Run(() =>
        {
            var called = false;
            ThumbnailCache.LoadAsync(_temp.GetFile("ghost.png"), 320, _sta.Dispatcher, _ => called = true);

            Assert.False(ThumbnailCache.TryGet(_temp.GetFile("ghost.png"), out _));
            Assert.False(called);
        });
    }

    [Fact]
    public void LoadAsync_解码失败的图片_不回调不崩溃()
    {
        _sta.Run(() =>
        {
            var badFile = _temp.GetFile("bad.png");
            File.WriteAllText(badFile, "这不是图片");

            using var done = new ManualResetEventSlim();
            var called = false;
            ThumbnailCache.LoadAsync(badFile, 320, _sta.Dispatcher, _ =>
            {
                called = true;
                done.Set();
            });

            // 等待 1.5 秒确认无回调（失败路径静默）
            Assert.False(done.Wait(TimeSpan.FromSeconds(1.5)));
            Assert.False(called);
        });
    }
}
