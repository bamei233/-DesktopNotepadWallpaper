using System.IO;
using DesktopNotepadWallpaper.Models;

namespace DesktopNotepadWallpaper.Services;

/// <summary>
/// 壁纸轮播状态机：索引 0 为记事本壁纸（固定第 1 张），1..N 对应图库图片。
/// </summary>
public sealed class WallpaperRotatorService
{
    private readonly AppConfig _config;
    private readonly GalleryService _gallery;
    private readonly INotepadRenderer _render;
    private readonly Func<IReadOnlyList<TaskItem>> _getTasks;
    private readonly IWallpaperSetter _setter;
    private readonly IWallpaperRenderExecutor _executor;

    public WallpaperRotatorService(
        AppConfig config,
        GalleryService gallery,
        INotepadRenderer render,
        Func<IReadOnlyList<TaskItem>> getTasks,
        IWallpaperSetter setter,
        IWallpaperRenderExecutor? executor = null)
    {
        _config = config;
        _gallery = gallery;
        _render = render;
        _getTasks = getTasks;
        _setter = setter;
        _executor = executor ?? InlineExecutor.Instance;
    }

    /// <summary>0 = 记事本壁纸，1..GalleryCount = 图库第 N 张。</summary>
    public int CurrentIndex { get; private set; }

    public int GalleryCount => _config.GalleryImages.Count;

    /// <summary>启动时调用：渲染记事本壁纸并设为桌面（第 1 张）。</summary>
    public void Initialize()
    {
        CurrentIndex = 0;
        RenderNotepad(apply: true);
    }

    /// <summary>下一张：只在图库之间循环（0→1，最后一张→1）。</summary>
    public void Next()
    {
        if (GalleryCount == 0)
        {
            RenderNotepad(apply: true);
            return;
        }
        CurrentIndex = CurrentIndex < 1 || CurrentIndex >= GalleryCount ? 1 : CurrentIndex + 1;
        ApplyCurrent();
    }

    /// <summary>上一张：只在图库之间循环（1→最后一张，0→最后一张）。</summary>
    public void Previous()
    {
        if (GalleryCount == 0)
        {
            RenderNotepad(apply: true);
            return;
        }
        CurrentIndex = CurrentIndex <= 1 ? GalleryCount : CurrentIndex - 1;
        ApplyCurrent();
    }

    /// <summary>一键切回记事本壁纸，始终渲染最新内容（PRD 同步逻辑）。</summary>
    public void ShowNotepad()
    {
        CurrentIndex = 0;
        RenderNotepad(apply: true);
    }

    /// <summary>设置变更后刷新记事本壁纸：当前显示记事本则立即应用，否则只更新缓存。</summary>
    public void RefreshNotepadWallpaper()
    {
        RenderNotepad(apply: CurrentIndex == 0);
    }

    /// <summary>图库增删改后调用：修正索引并刷新当前壁纸。</summary>
    public void OnGalleryChanged()
    {
        if (GalleryCount == 0)
        {
            CurrentIndex = 0;
            RenderNotepad(apply: true);
            return;
        }
        if (CurrentIndex > GalleryCount)
        {
            CurrentIndex = GalleryCount;
        }
        ApplyCurrent();
    }

    private void ApplyCurrent()
    {
        if (CurrentIndex <= 0)
        {
            RenderNotepad(apply: true);
            return;
        }
        var file = _config.GalleryImages[CurrentIndex - 1];
        var path = Path.Combine(_gallery.GalleryDir, file);
        if (!File.Exists(path))
        {
            CurrentIndex = 0;
            RenderNotepad(apply: true);
            return;
        }
        _executor.Execute(() => _setter.Set(path));
    }

    private void RenderNotepad(bool apply)
    {
        var tasks = _getTasks().ToList();
        _executor.Execute(() =>
        {
            var (ok, _) = _render.Render(tasks, _config);
            if (ok && apply)
            {
                _setter.Set(_render.CacheFilePath);
            }
        });
    }

    private sealed class InlineExecutor : IWallpaperRenderExecutor
    {
        public static InlineExecutor Instance { get; } = new();

        public void Execute(Action action)
        {
            action();
        }
    }
}
