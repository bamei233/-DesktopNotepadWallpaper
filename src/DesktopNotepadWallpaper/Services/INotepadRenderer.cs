using DesktopNotepadWallpaper.Models;

namespace DesktopNotepadWallpaper.Services;

public interface INotepadRenderer
{
    string CacheFilePath { get; }

    (bool Succeeded, string? Error) Render(IReadOnlyList<TaskItem> tasks, AppConfig config);
}
