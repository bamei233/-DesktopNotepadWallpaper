using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopNotepadWallpaper.Models;
using DesktopNotepadWallpaper.Services;
using HandyControl.Controls;
using Microsoft.Win32;

namespace DesktopNotepadWallpaper.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly GalleryService _gallery;
    private readonly DataService _data;
    private readonly AppConfig _config;
    private readonly WallpaperRotatorService _rotator;
    private readonly AutoStartService _autoStart;
    private readonly DispatcherTimer _debounce;
    private readonly ICommand _pickBackgroundImageCommand;
    private bool _dirty;

    // ---- 图库 ----
    public ObservableCollection<GalleryItem> Gallery { get; } = new();

    public ICommand ImportCommand { get; }

    public ICommand DeleteImageCommand { get; }

    public string CountText => $"图库 {Gallery.Count}/{GalleryService.MaxImages}";

    /// <summary>图库增删改后触发，主视图模型负责持久化与轮播刷新。</summary>
    public event Action? GalleryChanged;

    // ---- 系统 ----
    public ICommand BackupCommand { get; }

    public ICommand ResetDefaultsCommand { get; }

    /// <summary>用户确认恢复默认后触发，由主视图模型执行完整重置。</summary>
    public event Action? ResetRequested;

    public string VersionText => $"版本 {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    public SettingsViewModel(
        GalleryService gallery,
        DataService data,
        AppConfig config,
        WallpaperRotatorService rotator,
        AutoStartService autoStart)
    {
        _gallery = gallery;
        _data = data;
        _config = config;
        _rotator = rotator;
        _autoStart = autoStart;

        ImportCommand = new RelayCommand(Import);
        DeleteImageCommand = new RelayCommand<GalleryItem>(Delete);
        BackupCommand = new RelayCommand(Backup);
        ResetDefaultsCommand = new RelayCommand(AskResetDefaults);
        _pickBackgroundImageCommand = new RelayCommand(PickBackgroundImage);

        foreach (var family in Fonts.SystemFontFamilies.OrderBy(f => f.Source))
        {
            FontFamilies.Add(family.Source);
        }
        if (!FontFamilies.Contains(_config.Font.Family))
        {
            FontFamilies.Insert(0, _config.Font.Family);
        }

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            PersistAndRefresh();
        };

        Refresh();
        Reload();
    }

    // ---- 图库操作 ----
    private void Refresh()
    {
        Gallery.Clear();
        foreach (var name in _gallery.Images)
        {
            Gallery.Add(new GalleryItem(name, Path.Combine(_gallery.GalleryDir, name)));
        }
        OnPropertyChanged(nameof(CountText));
    }

    public void Move(GalleryItem source, int targetIndex)
    {
        var oldIndex = Gallery.IndexOf(source);
        if (oldIndex < 0)
        {
            return;
        }
        var insertIndex = targetIndex > oldIndex ? targetIndex - 1 : targetIndex;
        insertIndex = Math.Clamp(insertIndex, 0, Gallery.Count - 1);
        if (insertIndex == oldIndex)
        {
            return;
        }
        Gallery.Move(oldIndex, insertIndex);
        _gallery.Reorder(Gallery.Select(g => g.FileName));
        Persist();
    }

    private void Import()
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入图片到图库",
            Multiselect = true,
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.webp;*.bmp|所有文件|*.*"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        var (imported, rejected) = _gallery.ImportFiles(dialog.FileNames);
        if (imported > 0)
        {
            Persist();
            Refresh();
            Growl.SuccessGlobal($"已导入 {imported} 张图片");
        }
        if (rejected > 0)
        {
            Growl.WarningGlobal($"有 {rejected} 张未导入（超过 {GalleryService.MaxImages} 张上限或格式不支持）");
        }
    }

    private void Delete(GalleryItem? item)
    {
        if (item == null)
        {
            return;
        }
        _gallery.Delete(item.FileName);
        Persist();
        Refresh();
    }

    private void Persist()
    {
        _data.SaveConfig(_config);
        GalleryChanged?.Invoke();
    }

    // ---- 背景样式 ----
    public bool IsSolidStyle
    {
        get => _config.WallpaperBackground.Style == "Solid";
        set => SetBackgroundStyle(value, "Solid");
    }

    public bool IsGradientStyle
    {
        get => _config.WallpaperBackground.Style == "Gradient";
        set => SetBackgroundStyle(value, "Gradient");
    }

    public bool IsImageBlurStyle
    {
        get => _config.WallpaperBackground.Style == "ImageBlur";
        set => SetBackgroundStyle(value, "ImageBlur");
    }

    public SolidColorBrush SolidColorBrush
    {
        get => ToBrush(_config.WallpaperBackground.SolidColor);
        set => UpdateConfig(() => _config.WallpaperBackground.SolidColor = ToHex(value.Color));
    }

    public SolidColorBrush GradientStartBrush
    {
        get => ToBrush(_config.WallpaperBackground.GradientStart);
        set => UpdateConfig(() => _config.WallpaperBackground.GradientStart = ToHex(value.Color));
    }

    public SolidColorBrush GradientEndBrush
    {
        get => ToBrush(_config.WallpaperBackground.GradientEnd);
        set => UpdateConfig(() => _config.WallpaperBackground.GradientEnd = ToHex(value.Color));
    }

    public double BlurRadius
    {
        get => _config.WallpaperBackground.BlurRadius;
        set => UpdateConfig(() => _config.WallpaperBackground.BlurRadius = value);
    }

    // ---- 内容内边距 ----
    public double PaddingLeft
    {
        get => _config.Padding.Left;
        set => UpdateConfig(() => _config.Padding.Left = value);
    }

    public double PaddingTop
    {
        get => _config.Padding.Top;
        set => UpdateConfig(() => _config.Padding.Top = value);
    }

    public double PaddingRight
    {
        get => _config.Padding.Right;
        set => UpdateConfig(() => _config.Padding.Right = value);
    }

    public double PaddingBottom
    {
        get => _config.Padding.Bottom;
        set => UpdateConfig(() => _config.Padding.Bottom = value);
    }

    public string BgImageName => Path.GetFileName(_config.WallpaperBackground.ImagePath);

    public ICommand PickBackgroundImageCommand => _pickBackgroundImageCommand;

    private void PickBackgroundImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择背景图片",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.webp;*.bmp|所有文件|*.*"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        try
        {
            var backgroundDir = Path.Combine(_data.DataDir, "Background");
            Directory.CreateDirectory(backgroundDir);
            var name = "bg_" + Guid.NewGuid().ToString("N") + ".png";
            GalleryService.ConvertToPng(dialog.FileName, Path.Combine(backgroundDir, name));
            UpdateConfig(() => _config.WallpaperBackground.ImagePath = Path.Combine("Background", name));
            OnPropertyChanged(nameof(BgImageName));
        }
        catch (Exception ex)
        {
            Growl.ErrorGlobal($"背景图片处理失败: {ex.Message}");
        }
    }

    // ---- 字体 ----
    public ObservableCollection<string> FontFamilies { get; } = new();

    public string SelectedFontFamily
    {
        get => _config.Font.Family;
        set => UpdateConfig(() => _config.Font.Family = value);
    }

    public double BaseFontSize
    {
        get => _config.Font.BaseSize;
        set => UpdateConfig(() => _config.Font.BaseSize = Math.Max(8, Math.Round(value)));
    }

    public SolidColorBrush FontColorBrush
    {
        get => ToBrush(_config.Font.Color);
        set => UpdateConfig(() => _config.Font.Color = ToHex(value.Color));
    }

    // ---- 系统 ----
    public bool AutoStart
    {
        get => _autoStart.IsEnabled();
        set
        {
            _config.AutoStart = value;
            _autoStart.SetEnabled(value);
            _data.SaveConfig(_config);
        }
    }

    public bool MinimizeToTray
    {
        get => _config.MinimizeToTray;
        set => UpdateConfig(() => _config.MinimizeToTray = value);
    }

    private void Backup()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择备份保存位置"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        try
        {
            var dest = Path.Combine(dialog.FolderName, $"记事壁纸备份_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(dest);
            CopyIfExists(_data.TasksFilePath, dest);
            CopyIfExists(_data.ConfigFilePath, dest);
            CopyDirectory(_data.GalleryDir, Path.Combine(dest, "Gallery"));
            Growl.SuccessGlobal($"备份完成: {dest}");
        }
        catch (Exception ex)
        {
            Growl.ErrorGlobal($"备份失败: {ex.Message}");
        }
    }

    private void AskResetDefaults()
    {
        Growl.Ask("恢复默认设置将清空全部任务、图库与配置，确定吗？", ok =>
        {
            if (ok)
            {
                ResetRequested?.Invoke();
            }
            return true;
        });
    }

    /// <summary>配置被重置后，重新加载界面全部状态。</summary>
    public void Reload()
    {
        OnPropertyChanged(nameof(IsSolidStyle));
        OnPropertyChanged(nameof(IsGradientStyle));
        OnPropertyChanged(nameof(IsImageBlurStyle));
        OnPropertyChanged(nameof(SolidColorBrush));
        OnPropertyChanged(nameof(GradientStartBrush));
        OnPropertyChanged(nameof(GradientEndBrush));
        OnPropertyChanged(nameof(BlurRadius));
        OnPropertyChanged(nameof(BgImageName));
        OnPropertyChanged(nameof(PaddingLeft));
        OnPropertyChanged(nameof(PaddingTop));
        OnPropertyChanged(nameof(PaddingRight));
        OnPropertyChanged(nameof(PaddingBottom));
        OnPropertyChanged(nameof(SelectedFontFamily));
        OnPropertyChanged(nameof(BaseFontSize));
        OnPropertyChanged(nameof(FontColorBrush));
        OnPropertyChanged(nameof(AutoStart));
        OnPropertyChanged(nameof(MinimizeToTray));
        Refresh();
    }

    /// <summary>设置窗口关闭时调用，把防抖中未保存的修改落盘。</summary>
    public void Flush()
    {
        _debounce.Stop();
        PersistAndRefresh();
    }

    // ---- 内部工具 ----
    private void SetBackgroundStyle(bool selected, string style)
    {
        if (selected)
        {
            UpdateConfig(() => _config.WallpaperBackground.Style = style);
            OnPropertyChanged(nameof(IsSolidStyle));
            OnPropertyChanged(nameof(IsGradientStyle));
            OnPropertyChanged(nameof(IsImageBlurStyle));
        }
    }

    private void UpdateConfig(Action change)
    {
        change();
        _dirty = true;
        _debounce.Stop();
        _debounce.Start();
    }

    private void PersistAndRefresh()
    {
        if (!_dirty)
        {
            return;
        }
        _dirty = false;
        _data.SaveConfig(_config);
        _rotator.RefreshNotepadWallpaper();
    }

    private static SolidColorBrush ToBrush(string hex)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch
        {
            return Brushes.White;
        }
    }

    private static string ToHex(Color color)
    {
        return color.ToString();
    }

    private static void CopyIfExists(string file, string destDir)
    {
        if (File.Exists(file))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)));
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
