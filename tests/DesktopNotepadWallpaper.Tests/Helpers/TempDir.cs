using System.IO;

namespace DesktopNotepadWallpaper.Tests.Helpers;

/// <summary>临时目录，Dispose 时递归清理。</summary>
public sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "dnw_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string GetFile(string name)
    {
        return System.IO.Path.Combine(Path, name);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch
        {
            // 清理失败不阻断测试
        }
    }
}
