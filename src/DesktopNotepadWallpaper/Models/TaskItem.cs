using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DesktopNotepadWallpaper.Models;

public sealed class TaskItem : INotifyPropertyChanged
{
    private Guid _id = Guid.NewGuid();
    private string _description = string.Empty;
    private bool _isCompleted;
    private DateTime? _deadline;
    private List<string> _tags = new();
    private string _highlightColor = HighlightPalette.None;
    private int _sortIndex;

    public Guid Id { get => _id; set => Set(ref _id, value); }

    public string Description { get => _description; set => Set(ref _description, value); }

    public bool IsCompleted { get => _isCompleted; set => Set(ref _isCompleted, value); }

    public DateTime? Deadline
    {
        get => _deadline;
        set
        {
            if (Set(ref _deadline, value))
            {
                OnPropertyChanged(nameof(DeadlineDisplay));
            }
        }
    }

    public List<string> Tags
    {
        get => _tags;
        set
        {
            if (Set(ref _tags, value ?? new List<string>()))
            {
                OnPropertyChanged(nameof(TagText));
            }
        }
    }

    public string HighlightColor { get => _highlightColor; set => Set(ref _highlightColor, value); }

    public int SortIndex { get => _sortIndex; set => Set(ref _sortIndex, value); }

    /// <summary>色板弹窗是否打开（仅 UI 状态，不持久化）。</summary>
    [JsonIgnore]
    public bool IsPaletteOpen
    {
        get => _paletteOpen;
        set => Set(ref _paletteOpen, value);
    }

    private bool _paletteOpen;

    [JsonIgnore]
    public string TagText
    {
        get => string.Join(" ", _tags);
        set => Tags = (value ?? string.Empty)
            .Split(new[] { ' ', ',', '，', '、', ';', '；' }, StringSplitOptions.RemoveEmptyEntries)
            .Distinct()
            .ToList();
    }

    [JsonIgnore]
    public string DeadlineDisplay => _deadline is { } d
        ? $"截止: {d:yyyy-MM-dd HH:mm}"
        : "设置截止时间";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
