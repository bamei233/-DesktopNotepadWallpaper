using DesktopNotepadWallpaper.Models;
using DesktopNotepadWallpaper.Services;
using DesktopNotepadWallpaper.Tests.Fakes;
using DesktopNotepadWallpaper.Tests.Helpers;
using DesktopNotepadWallpaper.ViewModels;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.IO;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly DataService _data;
    private readonly WallpaperRenderService _render;
    private readonly AppConfig _config;
    private readonly GalleryService _gallery;
    private readonly FakeRenderer _fakeRenderer = new();
    private readonly FakeWallpaperSetter _fakeSetter = new();

    public MainViewModelTests()
    {
        _data = new DataService(_temp.Path);
        _render = new WallpaperRenderService(_data.CacheDir);
        _config = _data.LoadConfig();
        _gallery = new GalleryService(_data.GalleryDir, _config);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }

    private MainViewModel CreateViewModel()
    {
        var rotator = new WallpaperRotatorService(
            _config, _gallery, _fakeRenderer, () => new List<TaskItem>(), _fakeSetter);
        return new MainViewModel(_data, _render, rotator);
    }

    private string CreateDocx(params string[] paragraphs)
    {
        var path = _temp.GetFile($"import_{Guid.NewGuid():N}.docx");
        using (var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;
            foreach (var text in paragraphs)
            {
                body.AppendChild(new Paragraph(new Run(new Text(text))));
            }
            mainPart.Document.Save();
        }
        return path;
    }

    [Fact]
    public void ImportFromWord_解析段落_追加到任务列表并连续编号()
    {
        var vm = CreateViewModel();
        vm.AddTaskCommand.Execute(null);
        var existingMaxIndex = vm.Tasks.Max(t => t.SortIndex);
        var path = CreateDocx("导入任务一", "- 导入任务二", "1. 导入任务三");

        var count = vm.ImportFromWord(path);

        Assert.Equal(3, count);
        Assert.Equal(4, vm.Tasks.Count);
        var imported = vm.Tasks.Skip(1).ToList();
        Assert.Equal(new[] { "导入任务一", "导入任务二", "导入任务三" },
            imported.Select(t => t.Description));
        Assert.Equal(
            Enumerable.Range(existingMaxIndex + 1, 3),
            imported.Select(t => t.SortIndex));
    }

    [Fact]
    public void ImportFromWord_空文档_返回0且列表不变()
    {
        var vm = CreateViewModel();
        var path = CreateDocx("", "   ");

        var count = vm.ImportFromWord(path);

        Assert.Equal(0, count);
        Assert.Empty(vm.Tasks);
    }

    [Fact]
    public void ImportFromWord_文件不存在_抛出FileNotFoundException()
    {
        var vm = CreateViewModel();

        Assert.Throws<FileNotFoundException>(
            () => vm.ImportFromWord(_temp.GetFile("missing.docx")));
        Assert.Empty(vm.Tasks);
    }

    [Fact]
    public void 新增任务_SortIndex递增_描述为空且触发NewTaskAdded()
    {
        var vm = CreateViewModel();
        TaskItem? addedTask = null;
        vm.NewTaskAdded += t => addedTask = t;

        vm.AddTaskCommand.Execute(null);
        vm.AddTaskCommand.Execute(null);

        Assert.Equal(2, vm.Tasks.Count);
        Assert.Equal(0, vm.Tasks[0].SortIndex);
        Assert.Equal(1, vm.Tasks[1].SortIndex);
        Assert.Equal(string.Empty, vm.Tasks[0].Description);
        Assert.Equal(vm.Tasks[^1], addedTask);
    }

    [Fact]
    public void 删除任务_从列表移除()
    {
        var vm = CreateViewModel();
        vm.AddTaskCommand.Execute(null);
        var target = vm.Tasks[0];

        vm.DeleteTaskCommand.Execute(target);

        Assert.Empty(vm.Tasks);
    }

    [Fact]
    public void MoveTask_拖拽排序_重建SortIndex()
    {
        var vm = CreateViewModel();
        vm.AddTaskCommand.Execute(null);
        vm.AddTaskCommand.Execute(null);
        vm.AddTaskCommand.Execute(null);
        var first = vm.Tasks[0];

        vm.MoveTask(first, 3);

        Assert.Equal(first, vm.Tasks[^1]);
        Assert.Equal(new[] { 0, 1, 2 }, vm.Tasks.Select(t => t.SortIndex));
    }

    [Fact]
    public void 状态栏文本_反映任务与完成数()
    {
        var vm = CreateViewModel();
        vm.AddTaskCommand.Execute(null);
        vm.AddTaskCommand.Execute(null);
        vm.Tasks[0].IsCompleted = true;

        Assert.Contains("共 2 个事件", vm.StatusText);
        Assert.Contains("1 个未完成", vm.StatusText);
    }

    [Fact]
    public void SelectAll_两次调用_先全选再全不选()
    {
        var vm = CreateViewModel();
        vm.AddTaskCommand.Execute(null);
        vm.AddTaskCommand.Execute(null);
        vm.AddTaskCommand.Execute(null);

        vm.SelectAllCommand.Execute(null);

        Assert.All(vm.Tasks, t => Assert.True(t.IsSelected));
        Assert.True(vm.HasSelection);

        vm.SelectAllCommand.Execute(null);

        Assert.All(vm.Tasks, t => Assert.False(t.IsSelected));
        Assert.False(vm.HasSelection);
    }

    [Fact]
    public void DeleteSelected_只删除勾选的事件()
    {
        var vm = CreateViewModel();
        vm.AddTaskCommand.Execute(null);
        vm.AddTaskCommand.Execute(null);
        vm.AddTaskCommand.Execute(null);
        vm.Tasks[0].IsSelected = true;
        vm.Tasks[2].IsSelected = true;
        var survivor = vm.Tasks[1];

        vm.DeleteSelectedCommand.Execute(null);

        Assert.Single(vm.Tasks);
        Assert.Same(survivor, vm.Tasks[0]);
        Assert.False(vm.HasSelection);
    }

    [Fact]
    public void DeleteSelected_没有勾选_不删除任何事件()
    {
        var vm = CreateViewModel();
        vm.AddTaskCommand.Execute(null);

        vm.DeleteSelectedCommand.Execute(null);

        Assert.Single(vm.Tasks);
    }

    [Fact]
    public void HasSelection_随勾选状态实时更新()
    {
        var vm = CreateViewModel();
        vm.AddTaskCommand.Execute(null);
        Assert.False(vm.HasSelection);

        vm.Tasks[0].IsSelected = true;
        Assert.True(vm.HasSelection);

        vm.Tasks[0].IsSelected = false;
        Assert.False(vm.HasSelection);
    }
}
