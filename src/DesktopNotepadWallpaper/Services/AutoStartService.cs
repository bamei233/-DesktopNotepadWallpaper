using System.IO;
using System.Runtime.InteropServices;

namespace DesktopNotepadWallpaper.Services;

/// <summary>
/// 开机自启：在用户「启动」文件夹创建快捷方式（不写注册表）。
/// </summary>
public sealed class AutoStartService
{
    public const string ShortcutName = "记事壁纸 DesktopNotepadWallpaper.lnk";

    public string StartupFolder => Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    public string ShortcutPath => Path.Combine(StartupFolder, ShortcutName);

    public bool IsEnabled()
    {
        return File.Exists(ShortcutPath);
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            CreateShortcut();
        }
        else
        {
            TryDelete();
        }
    }

    private void CreateShortcut()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            return;
        }
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
        {
            return;
        }
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(ShortcutPath);
            shortcut.TargetPath = exePath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
            shortcut.Description = "记事壁纸 DesktopNotepadWallpaper";
            shortcut.Save();
        }
        finally
        {
            Marshal.ReleaseComObject(shell);
        }
    }

    private void TryDelete()
    {
        try
        {
            if (File.Exists(ShortcutPath))
            {
                File.Delete(ShortcutPath);
            }
        }
        catch (IOException)
        {
            // 被占用时静默跳过
        }
    }
}
