using System.Windows.Threading;
using Xunit;

namespace DesktopNotepadWallpaper.Tests.Helpers;

/// <summary>整个 UI 测试集合共享一个持久 STA 线程（避免 WPF 资源跨线程密封崩溃）。</summary>
public sealed class StaUiFixture : IDisposable
{
    private readonly Thread _thread;

    public StaUiFixture()
    {
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            Dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        });
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.IsBackground = true;
        _thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException("STA 测试线程启动超时");
        }
    }

    public Dispatcher Dispatcher { get; private set; } = null!;

    public void Run(Action action)
    {
        Dispatcher.Invoke(action);
    }

    public void Dispose()
    {
        Dispatcher.InvokeShutdown();
    }
}

[CollectionDefinition("UI")]
public sealed class UiCollection : ICollectionFixture<StaUiFixture>
{
}
