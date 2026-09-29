using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopNotepadWallpaper.Controls;
using DesktopNotepadWallpaper.Tests.Helpers;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

[Collection("UI")]
public sealed class ColorSwatchPickerUiTests
{
    private readonly StaUiFixture _sta;

    public ColorSwatchPickerUiTests(StaUiFixture sta)
    {
        _sta = sta;
    }

    private static void Pump()
    {
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
    }

    private static void EnsureApplicationResources()
    {
        var app = Application.Current ?? new Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
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
    }

    [Fact]
    public void 点击预设色块_更新SelectedBrush并关闭弹窗()
    {
        _sta.Run(() =>
        {
            EnsureApplicationResources();

            var picker = new ColorSwatchPicker { SelectedBrush = Brushes.White };
            var window = new Window
            {
                Content = picker,
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.Manual
            };
            window.Show();
            Pump();
            window.UpdateLayout();
            Pump();

            var toggle = (ToggleButton)picker.FindName("PART_Toggle");
            toggle.IsChecked = true;
            Pump();
            window.UpdateLayout();
            Pump();

            var grid = (Grid)VisualTreeHelper.GetParent(toggle);
            var popup = grid.Children.OfType<Popup>().First();
            Assert.True(popup.IsOpen, "色板弹窗应已打开");

            var uniformGrid = FindVisual<UniformGrid>(popup.Child!);
            Assert.NotNull(uniformGrid);
            var redButton = uniformGrid!.Children.OfType<Button>()
                .First(b => (string)b.Tag == "#F44336");

            redButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();

            Assert.Equal(Color.FromRgb(0xF4, 0x43, 0x36), picker.SelectedBrush.Color);
            Assert.False(popup.IsOpen, "选色后弹窗应关闭");

            window.Close();
        });
    }

    [Fact]
    public void 输入Hex并应用_更新SelectedBrush_非法值忽略()
    {
        _sta.Run(() =>
        {
            EnsureApplicationResources();

            var picker = new ColorSwatchPicker { SelectedBrush = Brushes.White };
            var window = new Window { Content = picker, Width = 400, Height = 300 };
            window.Show();
            Pump();
            window.UpdateLayout();
            Pump();

            var toggle = (ToggleButton)picker.FindName("PART_Toggle");
            var hexBox = (TextBox)picker.FindName("PART_HexBox");
            var grid = (Grid)VisualTreeHelper.GetParent(toggle);
            var popup = grid.Children.OfType<Popup>().First();
            var panel = (StackPanel)((Border)popup.Child!).Child;
            var dock = panel.Children.OfType<DockPanel>().First();
            var applyButton = dock.Children.OfType<Button>().First();

            toggle.IsChecked = true;
            Pump();
            window.UpdateLayout();
            Pump();
            hexBox.Text = "#00FF00";
            applyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            Assert.Equal(Color.FromRgb(0x00, 0xFF, 0x00), picker.SelectedBrush.Color);

            var before = picker.SelectedBrush.Color;
            toggle.IsChecked = true;
            Pump();
            hexBox.Text = "not-a-color";
            applyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            Assert.Equal(before, picker.SelectedBrush.Color);

            window.Close();
        });
    }

    private static T? FindVisual<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
            {
                return t;
            }
            if (FindVisual<T>(child) is { } result)
            {
                return result;
            }
        }
        return null;
    }
}
