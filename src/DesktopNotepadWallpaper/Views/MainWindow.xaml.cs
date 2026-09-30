using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopNotepadWallpaper.Converters;
using DesktopNotepadWallpaper.Helpers;
using DesktopNotepadWallpaper.Interop;
using DesktopNotepadWallpaper.Models;
using DesktopNotepadWallpaper.Services;
using DesktopNotepadWallpaper.ViewModels;
using HandyControl.Controls;

namespace DesktopNotepadWallpaper.Views;

public partial class MainWindow
{
    /// <summary>测试钩子：为 true 时跳过系统集成（全局热键、托盘图标），避免污染测试环境。</summary>
    public static bool DisableSystemIntegrations { get; set; }

    private GlobalHotkeyService? _hotkeys;
    private TrayService? _tray;
    private ContextMenu? _trayMenu;
    private bool _isReallyClosing;
    private bool _trayHintShown;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.NewTaskAdded += OnNewTaskAdded;
            }
        };
        BuildTrayMenu();
    }

    private void BuildTrayMenu()
    {
        var openItem = new MenuItem
        {
            Header = "打开主界面",
            Style = (Style)FindResource("TrayMenuItemStyle")
        };
        openItem.Click += (_, _) => RestoreFromTray();

        var exitItem = new MenuItem
        {
            Header = "退出程序",
            Style = (Style)FindResource("TrayMenuItemStyle")
        };
        exitItem.Click += (_, _) =>
        {
            _isReallyClosing = true;
            Close();
        };

        _trayMenu = new ContextMenu
        {
            Style = (Style)FindResource("TrayContextMenuStyle"),
            Placement = PlacementMode.MousePoint
        };
        _trayMenu.Items.Add(openItem);
        _trayMenu.Items.Add(new Separator { Style = (Style)FindResource("TraySeparatorStyle") });
        _trayMenu.Items.Add(exitItem);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!DisableSystemIntegrations)
        {
            _hotkeys = new GlobalHotkeyService(hwnd);
            _hotkeys.HotkeyPressed += OnHotkeyPressed;
            if (!_hotkeys.RegisterAll())
            {
                var conflicts = string.Join("、", _hotkeys.ConflictBindings.Select(b => b.DisplayText));
                Growl.WarningGlobal($"部分快捷键被其他程序占用: {conflicts}");
            }

            _tray = new TrayService(hwnd, CreateTrayIcon());
            _tray.RightClick += OnTrayRightClick;
            _tray.DoubleClick += RestoreFromTray;
            _tray.Add();
        }
        TryApplyAcrylicBackdrop(hwnd);
    }

    private void OnTrayRightClick()
    {
        _trayMenu?.SetCurrentValue(ContextMenu.IsOpenProperty, true);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        var minimizeToTray = DataContext is MainViewModel vm && vm.Config.MinimizeToTray;
        if (minimizeToTray && !_isReallyClosing)
        {
            e.Cancel = true;
            Hide();
            if (!_trayHintShown)
            {
                _trayHintShown = true;
                Growl.InfoGlobal("已最小化到托盘，双击托盘图标可重新打开");
            }
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.FlushSettings();
        }
        _hotkeys?.Dispose();
        _tray?.Dispose();
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    // ---- Win11 云母/亚克力背景材质（仅轻微质感；失败时保持纯色背景，不影响功能） ----
    private void TryApplyAcrylicBackdrop(IntPtr hwnd)
    {
        try
        {
            if (Environment.OSVersion.Version.Build < 22000)
            {
                return;
            }
            // Mica 比 Acrylic 柔和很多，配合接近不透明的白底只留一丝毛玻璃感
            var value = NativeMethods.DWMSBT_MAINWINDOW;
            if (NativeMethods.DwmSetWindowAttribute(
                    hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref value, sizeof(int)) == 0)
            {
                RootGrid.Background = new SolidColorBrush(Color.FromArgb(0xFB, 0xF7, 0xF7, 0xF9));
            }
        }
        catch
        {
            // 保持默认纯色背景
        }
    }

    // ---- 侧边栏导航与页面切换 ----
    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { IsChecked: true } radio)
        {
            return;
        }
        if (ReferenceEquals(radio, NavTask))
        {
            SwitchPage(0);
        }
        else if (ReferenceEquals(radio, NavGallery))
        {
            SwitchPage(1);
        }
        else if (ReferenceEquals(radio, NavSettings))
        {
            SwitchPage(2);
        }
    }

    private void SwitchPage(int page)
    {
        // XAML 解析期间字段可能尚未初始化（初始选中项触发 Checked），跳过即可
        if (TaskPage != null)
        {
            TaskPage.Visibility = page == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        if (GalleryPage != null)
        {
            GalleryPage.Visibility = page == 1 ? Visibility.Visible : Visibility.Collapsed;
        }
        if (SettingsPage != null)
        {
            SettingsPage.Visibility = page == 2 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void SwitchToTaskPage()
    {
        NavTask.IsChecked = true;
        SwitchPage(0);
    }

    private void OnNavAddTaskClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.AddTaskCommand.Execute(null);
        }
        SwitchToTaskPage();
    }

    private void OnNavImportWordClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "从 Word 导入事件",
            Filter = "Word 文档|*.docx"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        try
        {
            var count = vm.ImportFromWord(dialog.FileName);
            if (count > 0)
            {
                Growl.SuccessGlobal($"已从 Word 导入 {count} 个事件");
            }
            else
            {
                Growl.InfoGlobal("Word 文档中没有可导入的内容");
            }
        }
        catch (Exception ex)
        {
            Growl.ErrorGlobal($"Word 导入失败: {ex.Message}");
        }
        SwitchToTaskPage();
    }

    private void OnNavSaveClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SaveCommand.Execute(null);
        }
        SwitchToTaskPage();
    }

    private void OnHotkeyPressed(HotkeyAction action)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.HandleHotkey(action);
        }
    }

    // ---- 托盘 ----
    private void RestoreFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    private static ImageSource CreateTrayIcon()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(91, 91, 214)),
                null, new Rect(0, 0, 32, 32), 7, 7);
            var text = new FormattedText("记",
                CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"), 17, Brushes.White, 1.0);
            dc.DrawText(text, new Point((32 - text.Width) / 2, (32 - text.Height) / 2 - 1));
        }
        var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    // ---- 色板与标签 ----
    private void OnSwatchClick(object sender, RoutedEventArgs e)
    {
        // 色板条目携带任务引用（弹窗内逻辑树断裂，不能靠树查找）
        if (sender is Button { Tag: HighlightSwatchEntry entry })
        {
            entry.Task.HighlightColor = entry.Key;
            entry.Task.IsPaletteOpen = false;
        }
    }

    private void OnTagDoneClick(object sender, RoutedEventArgs e)
    {
        if (FindAncestor<Popup>((DependencyObject)sender) is { } popup)
        {
            popup.IsOpen = false;
        }
    }

    private void OnDeleteSelectedClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }
        var count = vm.Tasks.Count(t => t.IsSelected);
        if (count == 0)
        {
            return;
        }
        Growl.Ask($"确定删除选中的 {count} 个事件吗？", ok =>
        {
            if (ok)
            {
                vm.DeleteSelected();
            }
            return true;
        });
    }

    // ---- 事件实时丝滑拖拽排序 ----
    private TaskItem? _dragSource;
    private DragAdorner? _dragAdorner;
    private Point _dragStartPoint;
    private Point _grabOffset;
    private bool _isDragging;
    private int _pendingIndex;

    private void OnGripMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || DataContext is not MainViewModel)
        {
            return;
        }
        if (sender is not FrameworkElement { DataContext: TaskItem task })
        {
            return;
        }
        if (TaskList.ItemContainerGenerator.ContainerFromItem(task) is not FrameworkElement container)
        {
            return;
        }
        _dragSource = task;
        _isDragging = false;
        _dragStartPoint = e.GetPosition(TaskList);
        _grabOffset = e.GetPosition(container);
        _pendingIndex = TaskList.Items.IndexOf(task);
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void OnGripMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragSource == null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }
        var pos = e.GetPosition(TaskList);
        if (!_isDragging)
        {
            if (Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }
            _isDragging = true;
            if (TaskList.ItemContainerGenerator.ContainerFromItem(_dragSource) is FrameworkElement container)
            {
                _dragAdorner = new DragAdorner(TaskList, container,
                    new Size(container.ActualWidth, container.ActualHeight));
                AdornerLayer.GetAdornerLayer(TaskList)?.Add(_dragAdorner);
            }
        }
        if (_dragAdorner == null)
        {
            return;
        }
        _dragAdorner.SetPosition(new Point(pos.X - _grabOffset.X, pos.Y - _grabOffset.Y));
        var index = ComputeInsertionIndex(pos);
        _pendingIndex = index;
        _dragAdorner.SetInsertionLine(GetInsertionLineY(index));
        AutoScrollDuringDrag(e.GetPosition(TaskScroll));
    }

    private void OnGripMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragAdorner != null)
        {
            AdornerLayer.GetAdornerLayer(TaskList)?.Remove(_dragAdorner);
        }
        if (_isDragging && _dragSource != null && DataContext is MainViewModel vm)
        {
            var current = TaskList.Items.IndexOf(_dragSource);
            var target = _pendingIndex;
            if (target != current && target != current + 1)
            {
                vm.MoveTask(_dragSource, target);
            }
        }
        _dragAdorner = null;
        _dragSource = null;
        _isDragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
        e.Handled = true;
    }

    private double? GetInsertionLineY(int index)
    {
        var count = TaskList.Items.Count;
        if (index < count && TaskList.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement fe)
        {
            return fe.TranslatePoint(new Point(0, 0), TaskList).Y - 2;
        }
        if (count > 0 && TaskList.ItemContainerGenerator.ContainerFromIndex(count - 1) is FrameworkElement last)
        {
            var top = last.TranslatePoint(new Point(0, 0), TaskList).Y;
            return top + last.ActualHeight - 2;
        }
        return null;
    }

    private int ComputeInsertionIndex(Point pos)
    {
        var count = TaskList.Items.Count;
        for (var i = 0; i < count; i++)
        {
            if (TaskList.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement fe)
            {
                var top = fe.TranslatePoint(new Point(0, 0), TaskList).Y;
                if (pos.Y < top + fe.ActualHeight / 2)
                {
                    return i;
                }
            }
        }
        return count;
    }

    private void AutoScrollDuringDrag(Point scrollPos)
    {
        const double zone = 40;
        const double step = 14;
        if (scrollPos.Y < zone)
        {
            TaskScroll.ScrollToVerticalOffset(TaskScroll.VerticalOffset - step);
        }
        else if (scrollPos.Y > TaskScroll.ViewportHeight - zone)
        {
            TaskScroll.ScrollToVerticalOffset(TaskScroll.VerticalOffset + step);
        }
    }

    // ---- 手风琴展开/收起 ----
    private void OnAccordionHeaderClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string key })
        {
            return;
        }
        switch (key)
        {
            case "Background":
                ToggleAccordion(BackgroundContent, BackgroundChevron);
                break;
            case "Font":
                ToggleAccordion(FontContent, FontChevron);
                break;
            case "System":
                ToggleAccordion(SystemContent, SystemChevron);
                break;
            case "About":
                ToggleAccordion(AboutContent, AboutChevron);
                break;
        }
    }

    private static void ToggleAccordion(UIElement content, FrameworkElement chevron)
    {
        var show = content.Visibility != Visibility.Visible;
        content.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        chevron.RenderTransform = new RotateTransform(
            show ? 180 : 0, chevron.ActualWidth / 2, chevron.ActualHeight / 2);
    }

    // ---- 图库缩略图异步加载 ----
    private void OnGalleryThumbLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Image { Tag: string path } image || image.Source != null)
        {
            return;
        }
        if (ThumbnailCache.TryGet(path, out var cached))
        {
            image.Source = cached;
            return;
        }
        image.Source = ThumbnailCache.Placeholder;
        ThumbnailCache.LoadAsync(path, 320, Dispatcher, bitmap =>
        {
            if (image.Source == ThumbnailCache.Placeholder)
            {
                image.Source = bitmap;
            }
        });
    }

    // ---- 图库实时丝滑拖拽 ----
    private GalleryItem? _galleryDragSource;
    private DragAdorner? _galleryDragAdorner;
    private Point _galleryDragStart;
    private Point _galleryGrabOffset;
    private bool _galleryIsDragging;
    private int _galleryPendingIndex;

    private void OnGalleryPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        if (FindVisualAncestor<Button>((DependencyObject)e.OriginalSource) != null)
        {
            return;
        }
        var container = FindVisualAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (container?.DataContext is not GalleryItem item)
        {
            return;
        }
        _galleryDragSource = item;
        _galleryIsDragging = false;
        _galleryDragStart = e.GetPosition(GalleryList);
        _galleryGrabOffset = e.GetPosition(container);
        _galleryPendingIndex = GalleryList.Items.IndexOf(item);
        GalleryList.CaptureMouse();
        e.Handled = true;
    }

    private void OnGalleryPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_galleryDragSource == null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }
        var pos = e.GetPosition(GalleryList);
        if (!_galleryIsDragging)
        {
            if (Math.Abs(pos.X - _galleryDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _galleryDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }
            _galleryIsDragging = true;
            if (GalleryList.ItemContainerGenerator.ContainerFromItem(_galleryDragSource) is FrameworkElement container)
            {
                _galleryDragAdorner = new DragAdorner(GalleryList, container,
                    new Size(container.ActualWidth, container.ActualHeight));
                AdornerLayer.GetAdornerLayer(GalleryList)?.Add(_galleryDragAdorner);
            }
        }
        if (_galleryDragAdorner == null)
        {
            return;
        }
        _galleryDragAdorner.SetPosition(new Point(pos.X - _galleryGrabOffset.X, pos.Y - _galleryGrabOffset.Y));
        var index = GalleryComputeInsertionIndex(pos);
        _galleryPendingIndex = index;
        _galleryDragAdorner.SetInsertionLine(GalleryGetInsertionLineY(index));
    }

    private void OnGalleryPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        var wasDragging = _galleryIsDragging;
        if (_galleryDragAdorner != null)
        {
            AdornerLayer.GetAdornerLayer(GalleryList)?.Remove(_galleryDragAdorner);
        }
        if (wasDragging && _galleryDragSource != null &&
            DataContext is MainViewModel mainVm && mainVm.Settings is { } settings)
        {
            var current = GalleryList.Items.IndexOf(_galleryDragSource);
            var target = _galleryPendingIndex;
            if (target != current && target != current + 1)
            {
                settings.Move(_galleryDragSource, target);
            }
        }
        _galleryDragAdorner = null;
        _galleryDragSource = null;
        _galleryIsDragging = false;
        if (GalleryList.IsMouseCaptured)
        {
            GalleryList.ReleaseMouseCapture();
        }
        // 只在真实拖拽时吞掉鼠标事件，避免拦截缩略图删除按钮的点击
        e.Handled = wasDragging;
    }

    private double? GalleryGetInsertionLineY(int index)
    {
        var count = GalleryList.Items.Count;
        if (index < count && GalleryList.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement fe)
        {
            var center = fe.TranslatePoint(new Point(0, 0), GalleryList);
            return center.Y + fe.ActualHeight / 2;
        }
        if (count > 0 && GalleryList.ItemContainerGenerator.ContainerFromIndex(count - 1) is FrameworkElement last)
        {
            var center = last.TranslatePoint(new Point(0, 0), GalleryList);
            return center.Y + last.ActualHeight / 2;
        }
        return null;
    }

    private int GalleryComputeInsertionIndex(Point pos)
    {
        var count = GalleryList.Items.Count;
        var best = count;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < count; i++)
        {
            if (GalleryList.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement fe)
            {
                var center = fe.TranslatePoint(
                    new Point(fe.ActualWidth / 2, fe.ActualHeight / 2), GalleryList);
                var distance = (center - pos).Length;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
        }
        return best;
    }

    // ---- 新增事件聚焦 ----
    private void OnNewTaskAdded(TaskItem task)
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            if (TaskList.ItemContainerGenerator.ContainerFromItem(task) is not FrameworkElement container)
            {
                return;
            }
            container.BringIntoView();
            if (FindVisualChild<System.Windows.Controls.TextBox>(container) is { } textBox)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        });
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
            {
                return typed;
            }
            if (FindVisualChild<T>(child) is { } result)
            {
                return result;
            }
        }
        return null;
    }

    private static T? FindVisualAncestor<T>(DependencyObject obj) where T : class
    {
        while (obj != null && obj is not T)
        {
            obj = VisualTreeHelper.GetParent(obj);
        }
        return obj as T;
    }

    private static T? FindAncestor<T>(DependencyObject obj) where T : class
    {
        while (obj != null && obj is not T)
        {
            obj = LogicalTreeHelper.GetParent(obj);
        }
        return obj as T;
    }
}
