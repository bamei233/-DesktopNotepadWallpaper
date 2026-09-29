using System.IO;

namespace DesktopNotepadWallpaper.Services;

/// <summary>渲染/应用壁纸的执行器，生产环境用后台 STA 线程避免阻塞 UI。</summary>
public interface IWallpaperRenderExecutor
{
    void Execute(Action action);
}

/// <summary>每次渲染启动一个后台 STA 线程（WPF 渲染需要 STA），异常兜底防止进程崩溃。</summary>
public sealed class BackgroundStaRenderExecutor : IWallpaperRenderExecutor
{
    public void Execute(Action action)
    {
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        })
        {
            IsBackground = true,
            Name = "WallpaperRender"
        };
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
    }

    private static void LogError(Exception ex)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Data");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 壁纸后台线程异常: {ex}\n\n");
        }
        catch
        {
            // 日志失败不处理
        }
    }
}
