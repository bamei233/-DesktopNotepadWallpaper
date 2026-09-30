using System.Collections;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DesktopNotepadWallpaper.Converters;
using DesktopNotepadWallpaper.Models;
using DesktopNotepadWallpaper.Services;
using DesktopNotepadWallpaper.Tests.Fakes;
using DesktopNotepadWallpaper.Tests.Helpers;
using DesktopNotepadWallpaper.ViewModels;
using DesktopNotepadWallpaper.Views;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

/// <summary>
/// 主窗口 UI 集成测试（真实 XAML + 模拟交互）。
/// 与热键测试同集合，避免并行注册全局热键互相冲突。
/// </summary>
[Collection("UI")]
public sealed class MainWindowUiTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly StaUiFixture _sta;
    private Window? _window;

    public MainWindowUiTests(StaUiFixture sta)
    {
        _sta = sta;
    }

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

    private static void EnsureApplicationResources()
    {
        if (Application.Current != null)
        {
            return;
        }
        // 使用基础 Application + 手工合并主题与调色板（不能实例化真实 App：
        // 其 OnStartup 会在 Dispatcher 泵送时执行，创建真实主窗口并注册全局热键）
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml")
        });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Theme.xaml")
        });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/DesktopNotepadWallpaper;component/Resources/Palette.xaml")
        });
    }

    private MainWindow CreateWindow(out TaskItem task)
    {
        // 测试窗口不注册全局热键与托盘图标，避免与系统集成测试冲突
        MainWindow.DisableSystemIntegrations = true;
        var data = new DataService(_temp.Path);
        // 关闭最小化到托盘，测试结束时才能正常关闭窗口
        data.SaveConfig(new AppConfig { MinimizeToTray = false });
        var render = new WallpaperRenderService(data.CacheDir);
        var config = data.LoadConfig();
        var gallery = new GalleryService(data.GalleryDir, config);
        var tasks = new List<TaskItem>();
        var rotator = new WallpaperRotatorService(
            config, gallery, new FakeRenderer(), () => tasks, new FakeWallpaperSetter());
        var vm = new MainViewModel(data, render, rotator);
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
        _sta.Run(() =>
        {
            EnsureApplicationResources();

            var window = CreateWindow(out var task);
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
            var blueEntry = ((IEnumerable)swatches.ItemsSource)
                .Cast<HighlightSwatchEntry>()
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

    [Fact]
    public void 导航切换_图库页与设置页按预期显示隐藏()
    {
        _sta.Run(() =>
        {
            EnsureApplicationResources();

            var window = CreateWindow(out _);
            _window = window;
            window.Show();
            Pump();
            window.UpdateLayout();

            var taskPage = (Grid)window.FindName("TaskPage");
            var galleryPage = (Grid)window.FindName("GalleryPage");
            var settingsPage = (Grid)window.FindName("SettingsPage");
            Assert.Equal(Visibility.Visible, taskPage.Visibility);
            Assert.Equal(Visibility.Collapsed, galleryPage.Visibility);
            Assert.Equal(Visibility.Collapsed, settingsPage.Visibility);

            // 切到图库页
            var navGallery = (RadioButton)window.FindName("NavGallery");
            navGallery.IsChecked = true;
            Pump();
            Assert.Equal(Visibility.Collapsed, taskPage.Visibility);
            Assert.Equal(Visibility.Visible, galleryPage.Visibility);
            Assert.NotNull(window.FindName("GalleryList"));

            // 切到设置页，验证手风琴
            var navSettings = (RadioButton)window.FindName("NavSettings");
            navSettings.IsChecked = true;
            Pump();
            Assert.Equal(Visibility.Visible, settingsPage.Visibility);
            Assert.Equal(Visibility.Collapsed, galleryPage.Visibility);

            var bgHeader = (Button)window.FindName("BackgroundHeader");
            var bgContent = (Grid)window.FindName("BackgroundContent");
            Assert.NotNull(bgHeader);
            Assert.NotNull(bgContent);
            Assert.Equal(Visibility.Collapsed, bgContent.Visibility);

            // 手风琴标题文字必须可见（回归：标题不渲染问题）
            var titleTexts = FindVisuals<TextBlock>(bgHeader)
                .Where(t => t.Text == "背景样式")
                .ToList();
            Assert.NotEmpty(titleTexts);
            var title = titleTexts[0];
            window.UpdateLayout();
            Pump();
            Assert.True(title.ActualWidth > 0 && title.ActualHeight > 0,
                "手风琴标题必须实际渲染出可见尺寸");

            // 点击标题必须展开（AutomationPeer.Invoke 等价真实点击，会执行 Click 处理器）
            ((System.Windows.Automation.Provider.IInvokeProvider)
                new ButtonAutomationPeer(bgHeader)).Invoke();
            Pump();
            Assert.Equal(Visibility.Visible, bgContent.Visibility);
            // 再点一次收起
            ((System.Windows.Automation.Provider.IInvokeProvider)
                new ButtonAutomationPeer(bgHeader)).Invoke();
            Pump();
            Assert.Equal(Visibility.Collapsed, bgContent.Visibility);

            // 回到事件页
            var navTask = (RadioButton)window.FindName("NavTask");
            navTask.IsChecked = true;
            Pump();
            Assert.Equal(Visibility.Visible, taskPage.Visibility);

            window.Hide();
        });
    }

    [Fact]
    public void 图库缩略图删除按钮_点击后移除图片()
    {
        _sta.Run(() =>
        {
            EnsureApplicationResources();

            var window = CreateWindow(out _);
            _window = window;
            window.Show();
            Pump();
            window.UpdateLayout();

            var navGallery = (RadioButton)window.FindName("NavGallery");
            navGallery.IsChecked = true;
            Pump();
            window.UpdateLayout();
            Pump();

            var vm = (MainViewModel)window.DataContext;
            var settings = vm.Settings;
            settings.Gallery.Add(new GalleryItem("test_img.png",
                System.IO.Path.Combine(_temp.Path, "test_img.png")));

            var galleryList = (ListBox)window.FindName("GalleryList");
            Pump();
            window.UpdateLayout();
            Pump();
            var container = (FrameworkElement)galleryList.ItemContainerGenerator
                .ContainerFromItem(settings.Gallery[0])!;
            Assert.NotNull(container);

            var deleteButton = FindVisuals<Button>(container).First();
            Assert.NotNull(deleteButton.Command);
            // Invoke 等价真实点击：会执行命令（RaiseEvent 不会触发命令执行）
            ((System.Windows.Automation.Provider.IInvokeProvider)
                new ButtonAutomationPeer(deleteButton)).Invoke();
            Pump();

            Assert.Empty(settings.Gallery);

            window.Hide();
        });
    }

    [Fact]
    public void 标签编辑器_可输入空格与逗号_失焦后解析为标签()
    {
        _sta.Run(() =>
        {
            EnsureApplicationResources();

            var window = CreateWindow(out var task);
            _window = window;
            window.Show();
            Pump();
            window.UpdateLayout();

            var list = (ItemsControl)window.FindName("TaskList");
            var container = (FrameworkElement)list.ItemContainerGenerator.ContainerFromItem(task)!;
            Assert.NotNull(container);

            // 找到标签开关（弹窗子内容为 StackPanel 的才是标签弹窗）
            ToggleButton? tagToggle = null;
            Grid? grid = null;
            foreach (var candidate in FindVisuals<ToggleButton>(container))
            {
                if (VisualTreeHelper.GetParent(candidate) is Grid g &&
                    g.Children.OfType<Popup>().FirstOrDefault() is { } p &&
                    p.Child is Border b && b.Child is StackPanel)
                {
                    tagToggle = candidate;
                    grid = g;
                    break;
                }
            }
            Assert.NotNull(tagToggle);
            Assert.NotNull(grid);

            tagToggle!.IsChecked = true;
            Pump();
            window.UpdateLayout();
            Pump();

            var popup = grid!.Children.OfType<Popup>().First();
            Assert.True(popup.IsOpen, "标签弹窗应已打开");
            var panel = (StackPanel)((Border)popup.Child).Child;
            var textBox = panel.Children.OfType<TextBox>().First();
            var doneButton = panel.Children.OfType<Button>().Last();

            // 输入含空格和逗号的文本（不应被实时回写破坏）
            textBox.Focus();
            Pump();
            textBox.Text = "开发, 测试 重点";
            Assert.Equal("开发, 测试 重点", textBox.Text);

            // 焦点移走 → LostFocus 触发绑定写回（与真实点击「完成」等价）
            var moved = textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            Pump();

            Assert.True(moved, "焦点应成功移出输入框");
            Assert.Equal(new[] { "开发", "测试", "重点" }, task.Tags);
            Assert.Equal("开发 测试 重点", task.TagText);

            // 点击「完成」关闭弹窗
            doneButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            Assert.False(popup.IsOpen, "点击完成后弹窗应关闭");

            window.Hide();
        });
    }
}

