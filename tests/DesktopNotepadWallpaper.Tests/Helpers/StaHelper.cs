namespace DesktopNotepadWallpaper.Tests.Helpers;

/// <summary>在独立 STA 线程上执行 WPF 相关操作（RenderTargetBitmap、HwndSource 等需要 STA）。</summary>
public static class StaHelper
{
    public static T Run<T>(Func<T> func)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = func();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60)))
        {
            throw new TimeoutException("STA 线程执行超时");
        }
        if (error != null)
        {
            throw new Xunit.Sdk.XunitException($"STA 线程内抛出异常: {error}");
        }
        return result!;
    }

    public static void Run(Action action)
    {
        Run<object?>(() =>
        {
            action();
            return null;
        });
    }
}
