using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DesktopNotepadWallpaper.Helpers;

/// <summary>
/// 缩略图异步加载与缓存：后台线程解码并冻结位图，完成后回 UI 线程回调，
/// 避免大量大图在主线程同步解码造成界面卡死。
/// </summary>
public static class ThumbnailCache
{
    private static readonly ConcurrentDictionary<string, BitmapSource> Cache = new();
    private static readonly ConcurrentDictionary<string, List<Action<BitmapSource>>> Pending = new();

    private static readonly Lazy<BitmapSource> PlaceholderSource = new(CreatePlaceholder);

    public static BitmapSource Placeholder => PlaceholderSource.Value;

    public static bool TryGet(string path, out BitmapSource? bitmap)
    {
        return Cache.TryGetValue(path, out bitmap);
    }

    /// <summary>异步加载缩略图；缓存命中立即回调，加载完成后在 UI 调度器上回调。</summary>
    public static void LoadAsync(string path, int decodeWidth, Dispatcher uiDispatcher,
        Action<BitmapSource> onLoaded)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }
        if (Cache.TryGetValue(path, out var cached))
        {
            onLoaded(cached);
            return;
        }
        var callbacks = Pending.GetOrAdd(path, _ => new List<Action<BitmapSource>>());
        lock (callbacks)
        {
            callbacks.Add(onLoaded);
            if (callbacks.Count > 1)
            {
                return;
            }
        }
        Task.Run(() =>
        {
            BitmapSource? result = null;
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = decodeWidth;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                result = bitmap;
                Cache[path] = bitmap;
            }
            catch
            {
                // 解码失败：不回调，保持占位图
            }
            List<Action<BitmapSource>> toInvoke;
            lock (callbacks)
            {
                toInvoke = callbacks.ToList();
                callbacks.Clear();
            }
            Pending.TryRemove(path, out _);
            if (result != null && toInvoke.Count > 0)
            {
                uiDispatcher.BeginInvoke(() =>
                {
                    foreach (var callback in toInvoke)
                    {
                        callback(result);
                    }
                });
            }
        });
    }

    private static BitmapSource CreatePlaceholder()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF3)),
                null, new Rect(0, 0, 8, 8));
        }
        var bitmap = new RenderTargetBitmap(8, 8, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
