using System.Windows.Interop;
using DesktopNotepadWallpaper.Services;
using DesktopNotepadWallpaper.Tests.Helpers;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

[Collection("UI")]
public sealed class GlobalHotkeyServiceTests
{
    [Fact]
    public void 注册_再次注册冲突_注销后重新注册成功()
    {
        StaHelper.Run(() =>
        {
            using var source1 = new HwndSource(new HwndSourceParameters("dnw-hotkey-test-1"));
            using var service1 = new GlobalHotkeyService(source1.Handle);
            Assert.True(service1.RegisterAll());
            Assert.Empty(service1.ConflictBindings);

            // 同一组合被本进程占用 → 第二个服务注册全部冲突
            using var source2 = new HwndSource(new HwndSourceParameters("dnw-hotkey-test-2"));
            using var service2 = new GlobalHotkeyService(source2.Handle);
            Assert.False(service2.RegisterAll());
            Assert.Equal(3, service2.ConflictBindings.Count);
            Assert.Contains(service2.ConflictBindings, b => b.Action == HotkeyAction.PreviousWallpaper);
            Assert.Contains(service2.ConflictBindings, b => b.Action == HotkeyAction.NextWallpaper);
            Assert.Contains(service2.ConflictBindings, b => b.Action == HotkeyAction.ShowNotepad);

            // 第一个服务注销后，重新注册成功
            service1.UnregisterAll();
            Assert.True(service2.RegisterAll());
            Assert.Empty(service2.ConflictBindings);
            service2.UnregisterAll();
        });
    }

    [Fact]
    public void RegisterAll_重复调用_先注销旧注册再重新注册()
    {
        StaHelper.Run(() =>
        {
            using var source = new HwndSource(new HwndSourceParameters("dnw-hotkey-test-3"));
            using var service = new GlobalHotkeyService(source.Handle);

            Assert.True(service.RegisterAll());
            // 第二次调用自身先注销再注册，不应冲突
            Assert.True(service.RegisterAll());
            Assert.Empty(service.ConflictBindings);
        });
    }
}
