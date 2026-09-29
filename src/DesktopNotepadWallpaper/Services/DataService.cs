using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using DesktopNotepadWallpaper.Models;

namespace DesktopNotepadWallpaper.Services;

public sealed class DataService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    public DataService(string? dataDir = null)
    {
        DataDir = dataDir ?? Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(GalleryDir);
        Directory.CreateDirectory(CacheDir);
    }

    public string DataDir { get; }

    public string TasksFilePath => Path.Combine(DataDir, "tasks.json");

    public string ConfigFilePath => Path.Combine(DataDir, "config.json");

    public string GalleryDir => Path.Combine(DataDir, "Gallery");

    public string CacheDir => Path.Combine(DataDir, "Cache");

    public List<TaskItem> LoadTasks()
    {
        return Load<List<TaskItem>>(TasksFilePath) ?? new List<TaskItem>();
    }

    public void SaveTasks(IEnumerable<TaskItem> tasks)
    {
        Save(TasksFilePath, tasks.ToList());
    }

    public AppConfig LoadConfig()
    {
        return Load<AppConfig>(ConfigFilePath) ?? new AppConfig();
    }

    public void SaveConfig(AppConfig config)
    {
        Save(ConfigFilePath, config);
    }

    private static T? Load<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return default;
        }
    }

    private static void Save<T>(string path, T data)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, Options));
        File.Move(tmp, path, true);
    }
}
