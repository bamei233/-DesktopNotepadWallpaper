using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopNotepadWallpaper.Interop;

namespace DesktopNotepadWallpaper.Services;

/// <summary>
/// 自研系统托盘图标（Shell_NotifyIcon）：
/// HandyControl 的 NotifyIcon 右键菜单不可靠，改用 Win32 原生实现。
/// </summary>
public sealed class TrayService : IDisposable
{
    public const int TrayCallbackMessage = 0x8400;

    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int NIM_ADD = NativeMethods.NIM_ADD;
    private const int NIM_DELETE = NativeMethods.NIM_DELETE;

    private readonly HwndSource _source;
    private readonly IntPtr _iconHandle;
    private bool _added;

    /// <summary>托盘图标右键（触发时显示菜单）。</summary>
    public event Action? RightClick;

    /// <summary>托盘图标双击。</summary>
    public event Action? DoubleClick;

    public TrayService(IntPtr hwnd, ImageSource iconSource)
    {
        _source = HwndSource.FromHwnd(hwnd)
            ?? throw new InvalidOperationException("无法获取窗口句柄的 HwndSource");
        _iconHandle = CreateIconHandle(iconSource);
        _source.AddHook(WndProc);
    }

    public void Add()
    {
        var data = BuildData(NIM_ADD);
        if (NativeMethods.Shell_NotifyIcon(NIM_ADD, ref data))
        {
            _added = true;
        }
    }

    public void Remove()
    {
        if (!_added)
        {
            return;
        }
        var data = BuildData(NIM_DELETE);
        NativeMethods.Shell_NotifyIcon(NIM_DELETE, ref data);
        _added = false;
    }

    public void Dispose()
    {
        Remove();
        _source.RemoveHook(WndProc);
        NativeMethods.DestroyIcon(_iconHandle);
    }

    private NativeMethods.NOTIFYICONDATA BuildData(int message)
    {
        return new NativeMethods.NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
            hWnd = _source.Handle,
            uID = 1,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = TrayCallbackMessage,
            hIcon = _iconHandle,
            szTip = "记事壁纸"
        };
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == TrayCallbackMessage)
        {
            handled = true;
            switch (lParam.ToInt32())
            {
                case WM_RBUTTONUP:
                    RightClick?.Invoke();
                    break;
                case WM_LBUTTONDBLCLK:
                    DoubleClick?.Invoke();
                    break;
            }
        }
        return IntPtr.Zero;
    }

    private static IntPtr CreateIconHandle(ImageSource source)
    {
        if (source is not BitmapSource bitmapSource)
        {
            throw new ArgumentException("托盘图标必须是 BitmapSource");
        }
        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
        encoder.Save(stream);
        stream.Seek(0, SeekOrigin.Begin);
        using var bitmap = new Bitmap(stream);
        return bitmap.GetHicon();
    }
}
