using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DesktopNotepadWallpaper.Models;
using DesktopNotepadWallpaper.Services;
using DesktopNotepadWallpaper.Tests.Fakes;
using DesktopNotepadWallpaper.Tests.Helpers;
using DesktopNotepadWallpaper.ViewModels;
using DesktopNotepadWallpaper.Views;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

/// <summary>
/// 主窗口 UI 集成测试（真实 XAML + 模拟点击）。
/// 与热键测试同集合，避免并行注册全局热键互相冲突。
/// </summary>
[Collection("UI")]
public sealed class HighlightSwatchUiTests : IDisposable
{
    private readonly TempDir _temp = new();
    private Window? _window;

    public void Dispose()
    {
        _temp.Dispose();
        if (_window != null)
        {
            try
            {
                if (_window.IsVisible)
                {
                    _window.Hide();
                }
            }
            catch
            {
                // 窗口可能已关闭
            }
        }
    }

    private static void Pump()
    {
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
    }

    private MainWindow CreateWindow(out MainViewModel vm, out TaskItem task)
    {
        var data = new DataService(_temp.Path);
        // 关闭最小化到托盘，测试结束时才能正常关闭窗口
        data.SaveConfig(new AppConfig { MinimizeToTray = false });
        var render = new WallpaperRenderService(data.CacheDir);
        var config = data.LoadConfig();
        var gallery = new GalleryService(data.GalleryDir, config);
        var tasks = new List<TaskItem>();
        var rotator = new WallpaperRotatorService(
            config, gallery, new FakeRenderer(), () => tasks, new FakeWallpaperSetter());
        vm = new MainViewModel(data, render, rotator);
        var window = new MainWindow { DataContext = vm };
        vm.AddTaskCommand.Execute(null);
        task = vm.Tasks[0];
        return window;
    }

    private static IEnumerable<T> FindVisuals<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
            {
                yield return t;
            }
            foreach (var result in FindVisuals<T>(child))
            {
                yield return result;
            }
        }
    }

    [Fact]
    public void 点击色板_任务高亮色被设置_圆点与行背景同步变色()
    {
        StaHelper.Run(() =>
        {
            // 准备 WPF 应用资源（HandyControl 主题，MainWindow 样式依赖）
            var app = Application.Current ?? new Application();
            if (app.Resources.MergedDictionaries.Count == 0)
            {
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDark.xaml")
                });
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Theme.xaml")
                });
            }

            var window = CreateWindow(out _, out var task);
            _window = window;
            window.Show();
            Pump();
            window.UpdateLayout();

            // 找到任务行的容器与色板开关（弹窗子内容为 ItemsControl 的才是色板弹窗）
            var list = (ItemsControl)window.FindName("TaskList");
            var container = (FrameworkElement)list.ItemContainerGenerator.ContainerFromItem(task)!;
            Assert.NotNull(container);
            ToggleButton? toggle = null;
            Grid? grid = null;
            foreach (var candidate in FindVisuals<ToggleButton>(container))
            {
                if (VisualTreeHelper.GetParent(candidate) is Grid g &&
                    g.Children.OfType<Popup>().FirstOrDefault() is { } p &&
                    p.Child is Border && ((Border)p.Child).Child is ItemsControl)
                {
                    toggle = candidate;
                    grid = g;
                    break;
                }
            }
            Assert.NotNull(toggle);
            Assert.NotNull(grid);

            // 打开色板弹窗
            toggle!.IsChecked = true;
            Pump();
            window.UpdateLayout();
            Pump();

            var popup = grid!.Children.OfType<Popup>().First();
            Assert.True(popup.IsOpen, "色板弹窗应已打开");

            // 找到 "Blue" 色块并模拟点击
            var swatches = (ItemsControl)((Border)popup.Child).Child;
            var blueEntry = ((System.Collections.IEnumerable)swatches.ItemsSource)
                .Cast<DesktopNotepadWallpaper.Converters.HighlightSwatchEntry>()
                .First(e => e.Key == "Blue");
            Pump();
            var swatchContainer = (FrameworkElement)swatches.ItemContainerGenerator.ContainerFromItem(blueEntry)!;
            Assert.NotNull(swatchContainer);
            var blueButton = FindVisuals<Button>(swatchContainer).First();
            Assert.NotNull(blueButton);

            blueButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();

            // 断言：任务高亮色已更新，弹窗已关闭
            Assert.Equal("Blue", task.HighlightColor);
            Assert.False(popup.IsOpen, "选择颜色后弹窗应关闭");

            // 断言：圆点与行背景视觉同步
            var dot = FindVisuals<Ellipse>(toggle).FirstOrDefault();
            Assert.NotNull(dot);
            var expected = HighlightPalette.GetColor("Blue");
            Assert.Equal(expected.R, dot!.Fill is SolidColorBrush b1 ? b1.Color.R : (byte)0);

            var rowBorder = FindVisuals<Border>(container).First()!;
            var rowBrush = rowBorder.Background as SolidColorBrush;
            Assert.NotNull(rowBrush);
            Assert.Equal(40, rowBrush!.Color.A);
            Assert.Equal(HighlightPalette.GetColor("Blue").R, rowBrush.Color.R);

            window.Hide();
        });
    }
}
