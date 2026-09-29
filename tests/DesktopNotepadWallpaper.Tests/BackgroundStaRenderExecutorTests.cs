using DesktopNotepadWallpaper.Services;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

public sealed class BackgroundStaRenderExecutorTests
{
    [Fact]
    public void Execute_在STA后台线程执行动作()
    {
        var executor = new BackgroundStaRenderExecutor();
        ApartmentState? observed = null;
        var executed = false;
        using var done = new ManualResetEventSlim();
        executor.Execute(() =>
        {
            observed = Thread.CurrentThread.GetApartmentState();
            executed = true;
            done.Set();
        });

        Assert.True(done.Wait(TimeSpan.FromSeconds(10)), "执行器未在超时时间内执行动作");
        Assert.True(executed);
        Assert.Equal(ApartmentState.STA, observed);
    }

    [Fact]
    public void Execute_动作抛异常_被兜底且执行器仍可用()
    {
        var executor = new BackgroundStaRenderExecutor();
        using var done = new ManualResetEventSlim();
        executor.Execute(() =>
        {
            try
            {
                throw new InvalidOperationException("测试异常");
            }
            finally
            {
                done.Set();
            }
        });

        Assert.True(done.Wait(TimeSpan.FromSeconds(10)));
        // 执行器不受异常影响，仍可继续执行
        using var done2 = new ManualResetEventSlim();
        executor.Execute(() => done2.Set());
        Assert.True(done2.Wait(TimeSpan.FromSeconds(10)));
    }
}
