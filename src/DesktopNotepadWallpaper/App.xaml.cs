using System.IO;
using System.Windows;
using System.Windows.Threading;
using DesktopNotepadWallpaper.Services;
using DesktopNotepadWallpaper.ViewModels;
using DesktopNotepadWallpaper.Views;

namespace DesktopNotepadWallpaper;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        var data = new DataService();
        var render = new WallpaperRenderService(data.CacheDir);
        var viewModel = new MainViewModel(data, render);

        new MainWindow { DataContext = viewModel }.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogError(e.Exception);
        e.Handled = false;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogError(e.ExceptionObject as Exception);
    }

    private static void LogError(Exception? ex)
    {
        if (ex == null)
        {
            return;
        }
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Data");
            Directory.CreateDirectory(dir);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
            for (var current = ex; current != null; current = current.InnerException)
            {
                sb.AppendLine($"--- {current.GetType().FullName}: {current.Message}");
                if (current is System.Windows.Markup.XamlParseException xamlEx)
                {
                    sb.AppendLine($"    LineNumber={xamlEx.LineNumber} LinePosition={xamlEx.LinePosition} BaseUri={xamlEx.BaseUri}");
                }
                sb.AppendLine(current.StackTrace);
            }
            sb.AppendLine();
            File.AppendAllText(Path.Combine(dir, "error.log"), sb.ToString());
        }
        catch
        {
            // 日志失败不处理
        }
    }
}
