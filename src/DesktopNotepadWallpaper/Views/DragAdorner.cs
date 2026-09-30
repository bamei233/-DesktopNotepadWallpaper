using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DesktopNotepadWallpaper.Views;

/// <summary>拖拽时的半透明行预览（吸附在宿主控件的 Adorner 层）。</summary>
internal sealed class DragAdorner : Adorner
{
    private const double BrushOpacity = 0.9;

    private readonly VisualBrush _brush;
    private readonly Size _size;
    private Point _position;
    private double? _insertionY;

    public DragAdorner(UIElement adornedElement, Visual visual, Size size)
        : base(adornedElement)
    {
        _brush = new VisualBrush(visual)
        {
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            AutoLayoutContent = false
        };
        _size = size;
        IsHitTestVisible = false;
    }

    public void SetPosition(Point position)
    {
        _position = position;
        InvalidateVisual();
    }

    /// <summary>插入位置指示线（null 表示不显示）。</summary>
    public void SetInsertionLine(double? y)
    {
        _insertionY = y;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var rect = new Rect(_position, _size);
        drawingContext.PushOpacity(BrushOpacity);
        drawingContext.DrawRectangle(_brush,
            new Pen(new SolidColorBrush(Color.FromArgb(130, 91, 91, 214)), 1), rect);
        drawingContext.Pop();

        if (_insertionY is { } y)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(91, 91, 214)), 3)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            drawingContext.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
        }
    }
}
