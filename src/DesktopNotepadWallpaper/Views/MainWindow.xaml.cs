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
using DesktopNotepadWallpaper.Models;
using DesktopNotepadWallpaper.Services;
using DesktopNotepadWallpaper.ViewModels;
using HandyControl.Controls;

namespace DesktopNotepadWallpaper.Views;

public partial class MainWindow
{
    private GlobalHotkeyService? _hotkeys;
    private SettingsWindow? _settingsWindow;
    private bool _isReallyClosing;
    private bool _trayHintShown;

    public MainWindow()
    {
        InitializeComponent();
        TrayIcon.Icon = CreateTrayIcon();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.NewTaskAdded += OnNewTaskAdded;
            }
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hotkeys = new GlobalHotkeyService(new WindowInteropHelper(this).Handle);
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
        if (!_hotkeys.RegisterAll())
        {
            var conflicts = string.Join("、", _hotkeys.ConflictBindings.Select(b => b.DisplayText));
            Growl.WarningGlobal($"部分快捷键被其他程序占用: {conflicts}");
        }
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
        _hotkeys?.Dispose();
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    private void OnTrayOpenClick(object sender, RoutedEventArgs e)
    {
        RestoreFromTray();
    }

    private void OnTrayDoubleClick(object sender, RoutedEventArgs e)
    {
        RestoreFromTray();
    }

    private void OnTrayExitClick(object sender, RoutedEventArgs e)
    {
        _isReallyClosing = true;
        Close();
    }

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
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(53, 92, 125)),
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

    private void OnHotkeyPressed(HotkeyAction action)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.HandleHotkey(action);
        }
    }

    private void OnImportWordClick(object sender, RoutedEventArgs e)
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
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        ShowSettings(tabIndex: -1);
    }

    private void OnGalleryClick(object sender, RoutedEventArgs e)
    {
        ShowSettings(tabIndex: 0);
    }

    private void ShowSettings(int tabIndex)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }
        if (_settingsWindow is { IsVisible: true })
        {
            if (tabIndex >= 0)
            {
                _settingsWindow.SelectTab(tabIndex);
            }
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(vm.CreateSettingsViewModel())
        {
            Owner = this
        };
        if (tabIndex >= 0)
        {
            _settingsWindow.SelectTab(tabIndex);
        }
        _settingsWindow.Show();
    }

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

    // ---- 实时丝滑拖拽排序 ----
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

    private static T? FindAncestor<T>(DependencyObject obj) where T : class
    {
        while (obj != null && obj is not T)
        {
            obj = LogicalTreeHelper.GetParent(obj);
        }
        return obj as T;
    }
}
