using DesktopNotepadWallpaper.Models;
using Xunit;

namespace DesktopNotepadWallpaper.Tests;

public sealed class TaskItemTests
{
    [Fact]
    public void 默认值_符合规格()
    {
        var task = new TaskItem();

        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal(string.Empty, task.Description);
        Assert.False(task.IsCompleted);
        Assert.Null(task.Deadline);
        Assert.Empty(task.Tags);
        Assert.Equal(HighlightPalette.None, task.HighlightColor);
        Assert.Equal(0, task.SortIndex);
        Assert.Equal("设置截止时间", task.DeadlineDisplay);
    }

    [Theory]
    [InlineData("开发 测试", new[] { "开发", "测试" })]
    [InlineData("开发,测试", new[] { "开发", "测试" })]
    [InlineData("开发，测试", new[] { "开发", "测试" })]
    [InlineData("开发、测试", new[] { "开发", "测试" })]
    [InlineData("开发;测试；重点", new[] { "开发", "测试", "重点" })]
    public void TagText_多种分隔符_正确拆分(string input, string[] expected)
    {
        var task = new TaskItem { TagText = input };

        Assert.Equal(expected, task.Tags);
    }

    [Fact]
    public void TagText_重复标签_去重()
    {
        var task = new TaskItem { TagText = "开发 测试 开发" };

        Assert.Equal(new[] { "开发", "测试" }, task.Tags);
    }

    [Fact]
    public void TagText_空白输入_得到空标签集合()
    {
        var task = new TaskItem { Tags = new List<string> { "开发" } };

        task.TagText = "   ";

        Assert.Empty(task.Tags);
    }

    [Fact]
    public void TagText_读回_用空格连接()
    {
        var task = new TaskItem { Tags = new List<string> { "开发", "测试" } };

        Assert.Equal("开发 测试", task.TagText);
    }

    [Fact]
    public void DeadlineDisplay_有截止时间_格式化显示()
    {
        var task = new TaskItem { Deadline = new DateTime(2026, 9, 30, 18, 5, 0) };

        Assert.Equal("截止: 2026-09-30 18:05", task.DeadlineDisplay);
    }

    [Fact]
    public void 属性变更_触发PropertyChanged()
    {
        var task = new TaskItem();
        var changed = new List<string?>();
        task.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        task.Description = "新描述";
        task.IsCompleted = true;
        task.Deadline = DateTime.Now;

        Assert.Contains(nameof(TaskItem.Description), changed);
        Assert.Contains(nameof(TaskItem.IsCompleted), changed);
        Assert.Contains(nameof(TaskItem.Deadline), changed);
        Assert.Contains(nameof(TaskItem.DeadlineDisplay), changed);
    }

    [Fact]
    public void 设置相同值_不触发PropertyChanged()
    {
        var task = new TaskItem { Description = "不变" };
        var count = 0;
        task.PropertyChanged += (_, _) => count++;

        task.Description = "不变";

        Assert.Equal(0, count);
    }
}
