using DesktopNotepadWallpaper.Models;
using DesktopNotepadWallpaper.Services;

namespace DesktopNotepadWallpaper.Tests.Fakes;

public sealed class FakeRenderer : INotepadRenderer
{
    public int RenderCallCount { get; private set; }

    public string CacheFilePath { get; } = @"C:\fake\notepad_wallpaper.png";

    public (bool Succeeded, string? Error) Render(IReadOnlyList<TaskItem> tasks, AppConfig config)
    {
        RenderCallCount++;
        return (true, null);
    }
}

public sealed class FailingRenderer : INotepadRenderer
{
    public string CacheFilePath { get; } = @"C:\fake\notepad_wallpaper.png";

    public (bool Succeeded, string? Error) Render(IReadOnlyList<TaskItem> tasks, AppConfig config)
    {
        return (false, "渲染失败（测试用）");
    }
}

public sealed class FakeWallpaperSetter : IWallpaperSetter
{
    public List<string> AppliedPaths { get; } = new();

    public bool Set(string imagePath)
    {
        AppliedPaths.Add(imagePath);
        return true;
    }
}
