using System.IO;
using DesktopNotepadWallpaper.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace DesktopNotepadWallpaper.Services;

public sealed class GalleryService
{
    public const int MaxImages = 20;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp"
    };

    private readonly AppConfig _config;

    public GalleryService(string galleryDir, AppConfig config)
    {
        GalleryDir = galleryDir;
        _config = config;
        Directory.CreateDirectory(GalleryDir);
    }

    public string GalleryDir { get; }

    public IReadOnlyList<string> Images => _config.GalleryImages;

    /// <summary>多选导入：复制到 Gallery 目录（统一转 PNG），返回（成功数，拒绝数）。</summary>
    public (int Imported, int Rejected) ImportFiles(IEnumerable<string> paths)
    {
        var imported = 0;
        var rejected = 0;
        foreach (var path in paths)
        {
            if (_config.GalleryImages.Count >= MaxImages)
            {
                rejected++;
                continue;
            }
            if (!SupportedExtensions.Contains(Path.GetExtension(path)) || !File.Exists(path))
            {
                rejected++;
                continue;
            }
            try
            {
                var name = Guid.NewGuid().ToString("N") + ".png";
                ConvertToPng(path, Path.Combine(GalleryDir, name));
                _config.GalleryImages.Add(name);
                imported++;
            }
            catch
            {
                rejected++;
            }
        }
        return (imported, rejected);
    }

    public void Delete(string fileName)
    {
        _config.GalleryImages.Remove(fileName);
        TryDeleteFile(Path.Combine(GalleryDir, fileName));
    }

    public void Reorder(IEnumerable<string> orderedNames)
    {
        _config.GalleryImages.Clear();
        _config.GalleryImages.AddRange(orderedNames);
    }

    /// <summary>清空图库：删除全部文件并清空列表。</summary>
    public void ClearAll()
    {
        foreach (var name in _config.GalleryImages)
        {
            TryDeleteFile(Path.Combine(GalleryDir, name));
        }
        _config.GalleryImages.Clear();
    }

    /// <summary>任意格式图片转 PNG（ImageSharp 解码，含 webp，自动纠正 EXIF 方向）。</summary>
    public static void ConvertToPng(string sourcePath, string destPath)
    {
        using var image = Image.Load(sourcePath);
        image.Mutate(x => x.AutoOrient());
        image.Save(destPath, new PngEncoder());
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // 文件被占用时静默跳过，下次启动可再清理
        }
    }
}
