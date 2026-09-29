using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using DesktopNotepadWallpaper.Helpers;
using DesktopNotepadWallpaper.Models;
using DesktopNotepadWallpaper.Services;
using HandyControl.Controls;

namespace DesktopNotepadWallpaper.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly DataService _data;
    private readonly WallpaperRenderService _render;
    private readonly GalleryService _gallery;
    private readonly WallpaperRotatorService _rotator;
    private readonly AutoStartService _autoStart;
    private readonly IWallpaperSetter _wallpaperSetter;
    private readonly IWallpaperRenderExecutor _executor;
    private readonly Dispatcher _uiDispatcher;
    private readonly AppConfig _config;
    private SettingsViewModel? _settings;
    private string _statusText = "就绪";

    public ObservableCollection<TaskItem> Tasks { get; } = new();

    public ICommand AddTaskCommand { get; }

    public ICommand DeleteTaskCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand SelectAllCommand { get; }

    public ICommand DeleteSelectedCommand { get; }

    /// <summary>是否有事件处于选中状态（控制批量删除按钮）。</summary>
    public bool HasSelection
    {
        get => _hasSelection;
        private set
        {
            _hasSelection = value;
            OnPropertyChanged();
        }
    }

    private bool _hasSelection;

    /// <summary>新增事件后触发（UI 用于聚焦新行输入框）。</summary>
    public event Action<TaskItem>? NewTaskAdded;

    public string StatusText
    {
        get => _statusText;
        private set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public MainViewModel(DataService data, WallpaperRenderService render, WallpaperRotatorService? rotator = null)
    {
        _data = data;
        _render = render;
        _config = data.LoadConfig();
        _gallery = new GalleryService(data.GalleryDir, _config);
        _wallpaperSetter = new Win32WallpaperSetter();
        _executor = new BackgroundStaRenderExecutor();
        _uiDispatcher = Dispatcher.CurrentDispatcher;
        _rotator = rotator ?? new WallpaperRotatorService(
            _config, _gallery, render, () => Tasks, _wallpaperSetter, _executor);
        _autoStart = new AutoStartService();

        foreach (var task in data.LoadTasks().OrderBy(t => t.SortIndex))
        {
            Tasks.Add(task);
            AttachTask(task);
        }

        AddTaskCommand = new RelayCommand(AddTask);
        DeleteTaskCommand = new RelayCommand<TaskItem>(DeleteTask);
        SaveCommand = new RelayCommand(Save);
        SelectAllCommand = new RelayCommand(SelectAll);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected);

        _rotator.Initialize();
        UpdateStatus();
    }

    public AppConfig Config => _config;

    public SettingsViewModel CreateSettingsViewModel()
    {
        if (_settings == null)
        {
            _settings = new SettingsViewModel(_gallery, _data, _config, _rotator, _autoStart);
            _settings.GalleryChanged += OnGalleryChanged;
            _settings.ResetRequested += OnResetRequested;
        }
        return _settings;
    }

    public void HandleHotkey(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.PreviousWallpaper:
                _rotator.Previous();
                break;
            case HotkeyAction.NextWallpaper:
                _rotator.Next();
                break;
            case HotkeyAction.ShowNotepad:
                _rotator.ShowNotepad();
                break;
        }
        UpdateStatus();
    }

    private void OnGalleryChanged()
    {
        _data.SaveConfig(_config);
        _rotator.OnGalleryChanged();
        UpdateStatus();
    }

    private void OnResetRequested()
    {
        _config.WallpaperBackground = new BackgroundStyleConfig();
        _config.Font = new FontStyleConfig();
        _config.MinimizeToTray = true;
        _config.AutoStart = true;
        _autoStart.SetEnabled(true);
        _gallery.ClearAll();
        Tasks.Clear();

        _data.SaveTasks(Tasks);
        _data.SaveConfig(_config);
        _settings?.Reload();
        _rotator.OnGalleryChanged();
        UpdateStatus();
        Growl.SuccessGlobal("已恢复默认设置");
    }

    /// <summary>从 Word 文档导入事件，返回导入数量。</summary>
    public int ImportFromWord(string docxPath)
    {
        var imported = WordImportHelper.Parse(docxPath);
        var nextIndex = Tasks.Count == 0 ? 0 : Tasks.Max(t => t.SortIndex) + 1;
        foreach (var task in imported)
        {
            task.SortIndex = nextIndex++;
            Tasks.Add(task);
            AttachTask(task);
        }
        if (imported.Count > 0)
        {
            UpdateStatus();
        }
        return imported.Count;
    }

    private void AddTask()
    {
        var task = new TaskItem
        {
            SortIndex = Tasks.Count == 0 ? 0 : Tasks.Max(t => t.SortIndex) + 1
        };
        Tasks.Add(task);
        AttachTask(task);
        UpdateStatus();
        NewTaskAdded?.Invoke(task);
    }

    private void AttachTask(TaskItem task)
    {
        task.PropertyChanged += OnTaskPropertyChanged;
    }

    private void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TaskItem.IsCompleted))
        {
            UpdateStatus();
        }
        else if (e.PropertyName == nameof(TaskItem.IsSelected))
        {
            UpdateHasSelection();
        }
    }

    private void UpdateHasSelection()
    {
        HasSelection = Tasks.Any(t => t.IsSelected);
    }

    private void SelectAll()
    {
        var select = !Tasks.All(t => t.IsSelected);
        foreach (var task in Tasks)
        {
            task.IsSelected = select;
        }
        UpdateHasSelection();
    }

    /// <summary>删除全部选中事件。</summary>
    public void DeleteSelected()
    {
        var selected = Tasks.Where(t => t.IsSelected).ToList();
        foreach (var task in selected)
        {
            Tasks.Remove(task);
        }
        if (selected.Count > 0)
        {
            UpdateHasSelection();
            UpdateStatus();
        }
    }

    private void DeleteTask(TaskItem? task)
    {
        if (task != null && Tasks.Remove(task))
        {
            UpdateStatus();
        }
    }

    public void MoveTask(TaskItem source, int targetIndex)
    {
        var oldIndex = Tasks.IndexOf(source);
        if (oldIndex < 0)
        {
            return;
        }
        Tasks.RemoveAt(oldIndex);
        var insertIndex = targetIndex > oldIndex ? targetIndex - 1 : targetIndex;
        insertIndex = Math.Clamp(insertIndex, 0, Tasks.Count);
        Tasks.Insert(insertIndex, source);
        for (var i = 0; i < Tasks.Count; i++)
        {
            Tasks[i].SortIndex = i;
        }
    }

    private void Save()
    {
        _data.SaveTasks(Tasks);
        _data.SaveConfig(_config);
        var snapshot = Tasks.ToList();
        StatusText = "正在保存并渲染壁纸...";
        _executor.Execute(() =>
        {
            var (ok, error) = _render.Render(snapshot, _config);
            if (!ok)
            {
                SetStatusOnUi($"保存失败: {error}");
                return;
            }
            if (_rotator.CurrentIndex == 0)
            {
                _wallpaperSetter.Set(_render.CacheFilePath);
                SetStatusOnUi($"已保存并应用记事本壁纸 · {DateTime.Now:HH:mm:ss}");
            }
            else
            {
                SetStatusOnUi($"已保存，记事本壁纸缓存已更新（当前显示图库，切回记事本时生效）· {DateTime.Now:HH:mm:ss}");
            }
        });
    }

    private void SetStatusOnUi(string text)
    {
        if (_uiDispatcher.CheckAccess())
        {
            StatusText = text;
        }
        else
        {
            _uiDispatcher.BeginInvoke(() => StatusText = text);
        }
    }

    private void UpdateStatus()
    {
        var done = Tasks.Count(t => t.IsCompleted);
        var wallpaper = _rotator.CurrentIndex == 0
            ? "记事本"
            : $"图库 {_rotator.CurrentIndex}/{_rotator.GalleryCount}";
        StatusText = $"共 {Tasks.Count} 个事件 · {Tasks.Count - done} 个未完成 · 当前壁纸: {wallpaper}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
