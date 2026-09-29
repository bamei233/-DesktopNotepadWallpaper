using System;
using System.Collections.Generic;
using System.Windows.Interop;
using DesktopNotepadWallpaper.Interop;

namespace DesktopNotepadWallpaper.Services;

public enum HotkeyAction
{
    PreviousWallpaper,
    NextWallpaper,
    ShowNotepad
}

public sealed record HotkeyBinding(HotkeyAction Action, uint Modifiers, uint VirtualKey, string DisplayText);

public sealed class GlobalHotkeyService : IDisposable
{
    private const uint VK_LEFT = 0x25;
    private const uint VK_UP = 0x26;
    private const uint VK_RIGHT = 0x27;

    private static readonly IReadOnlyList<HotkeyBinding> DefaultBindings = new[]
    {
        new HotkeyBinding(HotkeyAction.PreviousWallpaper,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_ALT,
            VK_LEFT, "Ctrl + Shift + Alt + ←"),
        new HotkeyBinding(HotkeyAction.NextWallpaper,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_ALT,
            VK_RIGHT, "Ctrl + Shift + Alt + →"),
        new HotkeyBinding(HotkeyAction.ShowNotepad,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_ALT,
            VK_UP, "Ctrl + Shift + Alt + ↑"),
    };

    private const int HotkeyIdBase = 0x9001;

    private readonly HwndSource _source;
    private readonly List<int> _registeredIds = new();

    /// <summary>注册失败的快捷键（被其他程序占用）。</summary>
    public IReadOnlyList<HotkeyBinding> ConflictBindings { get; private set; } = Array.Empty<HotkeyBinding>();

    public event Action<HotkeyAction>? HotkeyPressed;

    public GlobalHotkeyService(IntPtr hwnd)
    {
        _source = HwndSource.FromHwnd(hwnd)
            ?? throw new InvalidOperationException("无法获取窗口的 HwndSource");
        _source.AddHook(WndProc);
    }

    /// <summary>注册全部快捷键，返回是否全部成功；失败项进 ConflictBindings。</summary>
    public bool RegisterAll()
    {
        UnregisterAll();
        var conflicts = new List<HotkeyBinding>();
        for (var i = 0; i < DefaultBindings.Count; i++)
        {
            var id = HotkeyIdBase + i;
            var binding = DefaultBindings[i];
            if (NativeMethods.RegisterHotKey(_source.Handle, id, binding.Modifiers, binding.VirtualKey))
            {
                _registeredIds.Add(id);
            }
            else
            {
                conflicts.Add(binding);
            }
        }
        ConflictBindings = conflicts;
        return conflicts.Count == 0;
    }

    public void UnregisterAll()
    {
        foreach (var id in _registeredIds)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, id);
        }
        _registeredIds.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var index = wParam.ToInt32() - HotkeyIdBase;
            if (index >= 0 && index < DefaultBindings.Count)
            {
                handled = true;
                HotkeyPressed?.Invoke(DefaultBindings[index].Action);
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _source.RemoveHook(WndProc);
        UnregisterAll();
    }
}
