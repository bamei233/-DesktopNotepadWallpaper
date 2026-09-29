using DesktopNotepadWallpaper.Helpers;
using DesktopNotepadWallpaper.Tests.Helpers;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.IO;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

public sealed class WordImportHelperTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    private string CreateDocx(params string[] paragraphTexts)
    {
        var path = _temp.GetFile($"doc_{Guid.NewGuid():N}.docx");
        using (var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;
            foreach (var text in paragraphTexts)
            {
                body.AppendChild(new Paragraph(new Run(new Text(text))));
            }
            mainPart.Document.Save();
        }
        return path;
    }

    [Fact]
    public void Parse_普通段落_逐条转为任务()
    {
        var path = CreateDocx("写周报", "参加评审会议", "整理 PRD");

        var tasks = WordImportHelper.Parse(path);

        Assert.Equal(3, tasks.Count);
        Assert.Equal(new[] { "写周报", "参加评审会议", "整理 PRD" }, tasks.Select(t => t.Description));
        Assert.Equal(new[] { 0, 1, 2 }, tasks.Select(t => t.SortIndex));
        Assert.All(tasks, t => Assert.NotEqual(Guid.Empty, t.Id));
    }

    [Fact]
    public void Parse_列表前缀_自动去除()
    {
        var path = CreateDocx(
            "- 项目符号任务",
            "• 圆点任务",
            "1. 数字编号任务",
            "2、顿号编号任务",
            "3) 括号编号任务",
            "[ ] markdown 未勾选",
            "[x] markdown 已勾选",
            "☐ 方框复选框",
            "☑ 已勾选复选框",
            "* 星号任务");

        var tasks = WordImportHelper.Parse(path);

        Assert.Equal(10, tasks.Count);
        Assert.Equal("项目符号任务", tasks[0].Description);
        Assert.Equal("圆点任务", tasks[1].Description);
        Assert.Equal("数字编号任务", tasks[2].Description);
        Assert.Equal("顿号编号任务", tasks[3].Description);
        Assert.Equal("括号编号任务", tasks[4].Description);
        Assert.Equal("markdown 未勾选", tasks[5].Description);
        Assert.Equal("markdown 已勾选", tasks[6].Description);
        Assert.Equal("方框复选框", tasks[7].Description);
        Assert.Equal("已勾选复选框", tasks[8].Description);
        Assert.Equal("星号任务", tasks[9].Description);
    }

    [Fact]
    public void Parse_空段落被跳过_纯空白段落不产生任务()
    {
        var path = CreateDocx("任务一", "", "   ", "任务二");

        var tasks = WordImportHelper.Parse(path);

        Assert.Equal(2, tasks.Count);
        Assert.Equal(new[] { "任务一", "任务二" }, tasks.Select(t => t.Description));
    }

    [Fact]
    public void Parse_只有前缀没有内容的段落_不产生任务()
    {
        var path = CreateDocx("- ", "1. ", "任务一");

        var tasks = WordImportHelper.Parse(path);

        Assert.Single(tasks);
        Assert.Equal("任务一", tasks[0].Description);
    }

    [Fact]
    public void Parse_文件不存在_抛出FileNotFoundException()
    {
        var missing = _temp.GetFile("not_exists.docx");

        var ex = Assert.Throws<FileNotFoundException>(() => WordImportHelper.Parse(missing));

        Assert.Equal(missing, ex.FileName);
    }

    [Fact]
    public void Parse_表格内段落_也被解析()
    {
        var path = _temp.GetFile($"table_{Guid.NewGuid():N}.docx");
        using (var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;
            var table = new Table(
                new TableRow(
                    new TableCell(new Paragraph(new Run(new Text("表格里的任务"))))));
            body.AppendChild(new Paragraph(new Run(new Text("正文任务"))));
            body.AppendChild(table);
            mainPart.Document.Save();
        }

        var tasks = WordImportHelper.Parse(path);

        Assert.Equal(2, tasks.Count);
        Assert.Equal(new[] { "正文任务", "表格里的任务" }, tasks.Select(t => t.Description));
    }
}
