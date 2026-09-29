using System.IO;
using System.Text.RegularExpressions;
using DesktopNotepadWallpaper.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DesktopNotepadWallpaper.Helpers;

/// <summary>
/// 从 Word 文档（.docx）解析事件：每个非空段落转为一条任务，
/// 自动去除项目符号、编号、复选框等列表前缀。
/// </summary>
public static class WordImportHelper
{
    private static readonly Regex CheckboxPrefix = new(
        @"^\s*(?:(?:[-*]\s*)?(?:\[[ xX✓✔]\]|[☐☑☒◻◼✓✔]))?\s*", RegexOptions.Compiled);

    private static readonly Regex BulletPrefix = new(
        @"^\s*(?:(?:[-*•·◦‣])|(?:\d+[.、)]))+\s*", RegexOptions.Compiled);

    public static IReadOnlyList<TaskItem> Parse(string docxPath)
    {
        if (!File.Exists(docxPath))
        {
            throw new FileNotFoundException("Word 文档不存在", docxPath);
        }

        var tasks = new List<TaskItem>();
        using var document = WordprocessingDocument.Open(docxPath, false);
        var body = document.MainDocumentPart?.Document.Body;
        if (body == null)
        {
            return tasks;
        }

        var index = 0;
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var text = paragraph.InnerText.Trim();
            if (text.Length == 0)
            {
                continue;
            }
            var cleaned = StripListPrefix(text);
            if (cleaned.Length == 0)
            {
                continue;
            }
            tasks.Add(new TaskItem { Description = cleaned, SortIndex = index++ });
        }
        return tasks;
    }

    private static string StripListPrefix(string text)
    {
        var cleaned = CheckboxPrefix.Replace(text, string.Empty);
        cleaned = BulletPrefix.Replace(cleaned, string.Empty);
        return cleaned.Trim();
    }
}
