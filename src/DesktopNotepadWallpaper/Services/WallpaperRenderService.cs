using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopNotepadWallpaper.Interop;
using DesktopNotepadWallpaper.Models;

namespace DesktopNotepadWallpaper.Services;

public sealed class WallpaperRenderService : INotepadRenderer
{
    public const string CacheFileName = "notepad_wallpaper.jpg";

    private const double RowSpacingDips = 16;
    private const double DeadlineScale = 0.62;
    private const double TagScale = 0.58;
    private const double HighlightAlpha = 88;

    private readonly string _cacheDir;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("zh-CN");

    public WallpaperRenderService(string cacheDir)
    {
        _cacheDir = cacheDir;
        Directory.CreateDirectory(cacheDir);
    }

    public string CacheFilePath => Path.Combine(_cacheDir, CacheFileName);

    /// <summary>渲染记事本壁纸 JPG 到 Cache 目录，返回成功与否和错误信息。</summary>
    public (bool Succeeded, string? Error) Render(
        IReadOnlyList<TaskItem> tasks, AppConfig config)
    {
        try
        {
            var pixelW = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            var pixelH = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
            var scale = NativeMethods.GetDpiForSystem() / 96.0;

            var padLeft = config.Padding.Left * scale;
            var padTop = config.Padding.Top * scale;
            var padRight = config.Padding.Right * scale;
            var padBottom = config.Padding.Bottom * scale;
            var contentW = pixelW - padLeft - padRight;
            var contentH = pixelH - padTop - padBottom;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                DrawBackground(dc, config, pixelW, pixelH, scale);
                var fontSize = FindFittingFontSize(tasks, config, contentW, contentH, scale);
                DrawTaskList(dc, tasks, config, fontSize, contentW, contentH, padLeft, padTop, scale);
            }

            var bitmap = new RenderTargetBitmap(pixelW, pixelH, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            SaveJpg(bitmap, CacheFilePath);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ---- 背景：纯色 / 渐变 / 模糊图片 ----
    private void DrawBackground(DrawingContext dc, AppConfig config, double w, double h, double scale)
    {
        var bg = config.WallpaperBackground;
        switch (bg.Style)
        {
            case "Solid":
                dc.DrawRectangle(ParseBrush(bg.SolidColor), null, new Rect(0, 0, w, h));
                break;

            case "Gradient":
                var brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };
                brush.GradientStops.Add(new GradientStop(ParseColor(bg.GradientStart), 0));
                brush.GradientStops.Add(new GradientStop(ParseColor(bg.GradientEnd), 1));
                dc.DrawRectangle(brush, null, new Rect(0, 0, w, h));
                break;

            case "ImageBlur":
                var image = LoadImage(ResolveDataPath(bg.ImagePath));
                if (image != null)
                {
                    var blurred = CreateBlurredCover(image, w, h, bg.BlurRadius);
                    var cover = CoverRect(blurred.PixelWidth, blurred.PixelHeight, w, h);
                    dc.DrawImage(blurred, cover);
                    dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)),
                        null, new Rect(0, 0, w, h));
                }
                else
                {
                    dc.DrawRectangle(Brushes.DarkSlateGray, null, new Rect(0, 0, w, h));
                }
                break;

            default:
                dc.DrawRectangle(Brushes.DarkSlateGray, null, new Rect(0, 0, w, h));
                break;
        }
    }

    // ---- 内容超出一屏时逐级减小字号，直到放下或到内部下限 ----
    private static readonly double MinFontSizeDips = 12;

    private double FindFittingFontSize(IReadOnlyList<TaskItem> tasks, AppConfig config,
        double contentW, double contentH, double scale)
    {
        var size = config.Font.BaseSize * scale;
        var min = MinFontSizeDips * scale;
        var step = 1.5 * scale;
        while (size > min)
        {
            if (MeasureContentHeight(tasks, config, size, contentW, scale) <= contentH)
            {
                return size;
            }
            size -= step;
        }
        return min;
    }

    private double MeasureContentHeight(IReadOnlyList<TaskItem> tasks, AppConfig config,
        double fontSize, double contentW, double scale)
    {
        var total = 0.0;
        foreach (var task in tasks)
        {
            total += MeasureRowHeight(task, config, fontSize, contentW, scale) + RowSpacingDips * scale;
        }
        return total;
    }

    private double MeasureRowHeight(TaskItem task, AppConfig config, double fontSize, double contentW, double scale)
    {
        var descH = CreateFormattedText(task.Description, config.Font.Family, fontSize, Colors.White, contentW).Height;
        var extra = task.Deadline != null || task.Tags.Count > 0 ? fontSize * 0.75 : 0;
        return descH + extra;
    }

    // ---- 任务列表绘制 ----
    private void DrawTaskList(DrawingContext dc, IReadOnlyList<TaskItem> tasks, AppConfig config,
        double fontSize, double contentW, double contentH, double padX, double padY, double scale)
    {
        var y = padY;
        var rowSpacing = RowSpacingDips * scale;
        var textColor = ParseColor(config.Font.Color);

        foreach (var task in tasks)
        {
            var rowHeight = MeasureRowHeight(task, config, fontSize, contentW, scale);

            // 高亮背景色块（圆角矩形）
            var highlight = HighlightPalette.GetColor(task.HighlightColor);
            if (highlight != Colors.Transparent)
            {
                var hl = Color.FromArgb((byte)HighlightAlpha, highlight.R, highlight.G, highlight.B);
                var hlRect = new Rect(padX, y - rowSpacing * 0.35, contentW, rowHeight + rowSpacing * 0.7);
                dc.DrawRoundedRectangle(new SolidColorBrush(hl), null, hlRect, 10 * scale, 10 * scale);
            }

            // 复选框：手动画方框 + 对勾，避免字体缺字形
            var checkSize = fontSize * 0.7;
            var boxRect = new Rect(padX, y + fontSize * 0.15, checkSize, checkSize);
            var strokeW = Math.Max(1.5, fontSize * 0.06);
            var borderPen = new Pen(new SolidColorBrush(textColor), strokeW);
            dc.DrawRoundedRectangle(null, borderPen, boxRect, 4, 4);
            if (task.IsCompleted)
            {
                var checkPen = new Pen(new SolidColorBrush(textColor), strokeW)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                dc.DrawLine(checkPen,
                    new Point(boxRect.Left + checkSize * 0.22, boxRect.Top + checkSize * 0.52),
                    new Point(boxRect.Left + checkSize * 0.44, boxRect.Top + checkSize * 0.74));
                dc.DrawLine(checkPen,
                    new Point(boxRect.Left + checkSize * 0.44, boxRect.Top + checkSize * 0.74),
                    new Point(boxRect.Left + checkSize * 0.80, boxRect.Top + checkSize * 0.28));
            }

            // 任务描述（完成后加删除线）
            var x = padX + checkSize + fontSize * 0.55;
            var desc = CreateFormattedText(task.Description, config.Font.Family, fontSize, textColor, contentW);
            if (task.IsCompleted)
            {
                desc.SetTextDecorations(TextDecorations.Strikethrough);
            }
            dc.DrawText(desc, new Point(x, y));

            // Deadline（过期显示红色）
            var smallY = y + desc.Height + 4 * scale;
            var nextX = x;
            if (task.Deadline is { } deadline)
            {
                var deadlineColor = deadline < DateTime.Now && !task.IsCompleted
                    ? Color.FromRgb(255, 110, 100)
                    : textColor;
                var deadlineText = $"截止: {deadline.ToString("yyyy-MM-dd HH:mm", _culture)}";
                var t = CreateFormattedText(deadlineText, config.Font.Family,
                    fontSize * DeadlineScale, deadlineColor, contentW);
                dc.DrawText(t, new Point(nextX, smallY));
                nextX += t.Width + 14 * scale;
            }

            // 标签（圆角小 chip）
            foreach (var tag in task.Tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }
                var tagText = $"# {tag}";
                var tf = CreateFormattedText(tagText, config.Font.Family,
                    fontSize * TagScale, textColor, contentW);
                var chipRect = new Rect(nextX, smallY - 2 * scale,
                    tf.Width + 12 * scale, tf.Height + 6 * scale);
                var chipBrush = new SolidColorBrush(Color.FromArgb(40, textColor.R, textColor.G, textColor.B));
                dc.DrawRoundedRectangle(chipBrush, null, chipRect, tf.Height / 2, tf.Height / 2);
                dc.DrawText(tf, new Point(nextX + 6 * scale, smallY + scale));
                nextX += chipRect.Width + 6 * scale;
            }

            y += rowHeight + rowSpacing;
            if (y > contentH + padY)
            {
                break;
            }
        }
    }

    // ---- 工具方法 ----
    private FormattedText CreateFormattedText(string text, string family, double size,
        Color color, double maxWidth)
    {
        return new FormattedText(
            text, _culture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily(family), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            size, new SolidColorBrush(color), 1.0)
        {
            MaxTextWidth = maxWidth,
            MaxLineCount = 100
        };
    }

    /// <summary>Data 目录下的相对路径转绝对路径。</summary>
    private string ResolveDataPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }
        var dataDir = Path.GetFullPath(Path.Combine(_cacheDir, ".."));
        return Path.GetFullPath(Path.Combine(dataDir, relativePath));
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.White;
        }
    }

    private static SolidColorBrush ParseBrush(string hex)
    {
        return new SolidColorBrush(ParseColor(hex));
    }

    private static BitmapImage LoadImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null!;
        }
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path, UriKind.Absolute);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private static Rect CoverRect(double imgW, double imgH, double targetW, double targetH)
    {
        var s = Math.Max(targetW / imgW, targetH / imgH);
        var w = imgW * s;
        var h = imgH * s;
        return new Rect((targetW - w) / 2, (targetH - h) / 2, w, h);
    }

    /// <summary>缩小再放大的近似模糊：不依赖 GPU Effect，RenderTargetBitmap 渲染结果稳定。</summary>
    private static BitmapSource CreateBlurredCover(BitmapSource source, double targetW, double targetH, double radius)
    {
        var down = Math.Clamp(4.0 / Math.Max(radius, 1.0), 0.03, 0.25);
        var smallW = Math.Max(8, (int)(source.PixelWidth * down));
        var smallH = Math.Max(8, (int)(source.PixelHeight * down));
        var small = new TransformedBitmap(source,
            new ScaleTransform((double)smallW / source.PixelWidth, (double)smallH / source.PixelHeight));
        var upScale = Math.Max(targetW / smallW, targetH / smallH);
        var blurred = new TransformedBitmap(small, new ScaleTransform(upScale, upScale));
        blurred.Freeze();
        return blurred;
    }

    /// <summary>JPG 编码速度远快于 PNG，且 Windows 设置壁纸时无需再转码。</summary>
    private static void SaveJpg(RenderTargetBitmap bitmap, string path)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
