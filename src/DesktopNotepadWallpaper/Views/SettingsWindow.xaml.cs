using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DesktopNotepadWallpaper.ViewModels;

namespace DesktopNotepadWallpaper.Views;

public partial class SettingsWindow
{
    private GalleryItem? _dragSource;
    private DragAdorner? _dragAdorner;
    private Point _dragStartPoint;
    private Point _grabOffset;
    private bool _isDragging;
    private int _pendingIndex;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>切换到指定索引的设置页（0=图库管理）。</summary>
    public void SelectTab(int index)
    {
        if (index >= 0 && index < SettingsTabs.Items.Count)
        {
            SettingsTabs.SelectedIndex = index;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.Flush();
        }
        base.OnClosed(e);
    }

    // ---- 图库实时丝滑拖拽 ----
    private void OnGalleryPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || DataContext is not SettingsViewModel)
        {
            return;
        }
        // 点击删除按钮不触发拖拽
        if (FindVisualAncestor<Button>((DependencyObject)e.OriginalSource) != null)
        {
            return;
        }
        var container = FindVisualAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (container?.DataContext is not GalleryItem item)
        {
            return;
        }
        _dragSource = item;
        _isDragging = false;
        _dragStartPoint = e.GetPosition(GalleryList);
        _grabOffset = e.GetPosition(container);
        _pendingIndex = GalleryList.Items.IndexOf(item);
        GalleryList.CaptureMouse();
        e.Handled = true;
    }

    private void OnGalleryPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragSource == null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }
        var pos = e.GetPosition(GalleryList);
        if (!_isDragging)
        {
            if (Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }
            _isDragging = true;
            if (GalleryList.ItemContainerGenerator.ContainerFromItem(_dragSource) is FrameworkElement container)
            {
                _dragAdorner = new DragAdorner(GalleryList, container,
                    new Size(container.ActualWidth, container.ActualHeight));
                AdornerLayer.GetAdornerLayer(GalleryList)?.Add(_dragAdorner);
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
    }

    private void OnGalleryPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragAdorner != null)
        {
            AdornerLayer.GetAdornerLayer(GalleryList)?.Remove(_dragAdorner);
        }
        if (_isDragging && _dragSource != null && DataContext is SettingsViewModel vm)
        {
            var current = GalleryList.Items.IndexOf(_dragSource);
            var target = _pendingIndex;
            if (target != current && target != current + 1)
            {
                vm.Move(_dragSource, target);
            }
        }
        _dragAdorner = null;
        _dragSource = null;
        _isDragging = false;
        GalleryList.ReleaseMouseCapture();
        e.Handled = true;
    }

    private double? GetInsertionLineY(int index)
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

    private int ComputeInsertionIndex(Point pos)
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

    private static T? FindVisualAncestor<T>(DependencyObject obj) where T : class
    {
        while (obj != null && obj is not T)
        {
            obj = VisualTreeHelper.GetParent(obj);
        }
        return obj as T;
    }
}
