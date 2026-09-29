using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopNotepadWallpaper.Controls;

/// <summary>
/// 自定义颜色选择控件：颜色块 + 预设色板弹窗 + 自定义 Hex 输入。
/// （HandyControl 3.5.1 的 ColorPicker 交互失效，改用自研实现。）
/// </summary>
public partial class ColorSwatchPicker : UserControl
{
    public static readonly DependencyProperty SelectedBrushProperty = DependencyProperty.Register(
        nameof(SelectedBrush), typeof(SolidColorBrush), typeof(ColorSwatchPicker),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public SolidColorBrush SelectedBrush
    {
        get => (SolidColorBrush)GetValue(SelectedBrushProperty);
        set => SetValue(SelectedBrushProperty, value);
    }

    public ColorSwatchPicker()
    {
        InitializeComponent();
    }

    private void OnPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex })
        {
            TryApply(hex);
            PART_Toggle.IsChecked = false;
        }
    }

    private void OnApplyHexClick(object sender, RoutedEventArgs e)
    {
        TryApply(PART_HexBox.Text);
        PART_Toggle.IsChecked = false;
    }

    private void TryApply(string value)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(value);
            SelectedBrush = new SolidColorBrush(color);
        }
        catch
        {
            // 非法颜色值忽略
        }
    }
}
