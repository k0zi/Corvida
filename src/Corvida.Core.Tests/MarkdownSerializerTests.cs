using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

public class MarkdownSerializerTests
{
    private static readonly DateTime FixedDate = new(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private static KanbanTask MakeTask() => new()
    {
        Id = "task-123",
        Title = "My Title",
        GroupId = "grp-1",
        BoardId = "board-1",
        Created = FixedDate,
        Priority = "High",
        Description = "A description"
    };

    [Fact]
    public void Serialize_OutputStartsWithFrontmatterDelimiter()
    {
        var output = MarkdownSerializer.Serialize(MakeTask());
        Assert.StartsWith("---", output);
    }

    [Fact]
    public void Serialize_FrontmatterContainsId()
    {
        var task = MakeTask();
        var output = MarkdownSerializer.Serialize(task);
        Assert.Contains($"id: {task.Id}", output);
    }

    [Fact]
    public void Serialize_FrontmatterContainsTitle()
    {
        var task = MakeTask();
        var output = MarkdownSerializer.Serialize(task);
        Assert.Contains($"title: {task.Title}", output);
    }

    [Fact]
    public void Serialize_FrontmatterContainsGroupId()
    {
        var task = MakeTask();
        var output = MarkdownSerializer.Serialize(task);
        Assert.Contains($"groupId: {task.GroupId}", output);
    }

    [Fact]
    public void Serialize_FrontmatterContainsBoardId()
    {
        var task = MakeTask();
        var output = MarkdownSerializer.Serialize(task);
        Assert.Contains($"boardId: {task.BoardId}", output);
    }

    [Fact]
    public void Serialize_FrontmatterContainsPriority()
    {
        var task = MakeTask();
        var output = MarkdownSerializer.Serialize(task);
        Assert.Contains($"priority: {task.Priority}", output);
    }

    [Fact]
    public void Serialize_FrontmatterContainsCreatedInIso8601()
    {
        var task = MakeTask();
        var output = MarkdownSerializer.Serialize(task);
        Assert.Contains($"created: {task.Created:O}", output);
    }

    [Fact]
    public void Serialize_OmitsPlannedStartWhenNull()
    {
        var task = MakeTask();
        task.PlannedStart = null;
        Assert.DoesNotContain("plannedStart:", MarkdownSerializer.Serialize(task));
    }

    [Fact]
    public void Serialize_OmitsPlannedEndWhenNull()
    {
        var task = MakeTask();
        task.PlannedEnd = null;
        Assert.DoesNotContain("plannedEnd:", MarkdownSerializer.Serialize(task));
    }

    [Fact]
    public void Serialize_IncludesPlannedStartWhenSet()
    {
        var task = MakeTask();
        task.PlannedStart = FixedDate;
        Assert.Contains($"plannedStart: {task.PlannedStart.Value:O}", MarkdownSerializer.Serialize(task));
    }

    [Fact]
    public void Serialize_IncludesPlannedEndWhenSet()
    {
        var task = MakeTask();
        task.PlannedEnd = FixedDate.AddDays(7);
        Assert.Contains($"plannedEnd: {task.PlannedEnd.Value:O}", MarkdownSerializer.Serialize(task));
    }

    [Fact]
    public void Serialize_DescriptionAppearsAfterFrontmatter()
    {
        var task = MakeTask();
        task.Description = "My description";
        var output = MarkdownSerializer.Serialize(task);

        var firstDelim = output.IndexOf("---", StringComparison.Ordinal);
        var secondDelim = output.IndexOf("---", firstDelim + 3, StringComparison.Ordinal);
        var descPos = output.IndexOf("My description", StringComparison.Ordinal);

        Assert.True(secondDelim >= 0);
        Assert.True(descPos > secondDelim);
    }

    [Fact]
    public void Parse_RoundtripsAllRequiredFields()
    {
        var original = MakeTask();
        var parsed = MarkdownSerializer.Parse(MarkdownSerializer.Serialize(original));

        Assert.Equal(original.Id, parsed.Id);
        Assert.Equal(original.Title, parsed.Title);
        Assert.Equal(original.GroupId, parsed.GroupId);
        Assert.Equal(original.BoardId, parsed.BoardId);
        Assert.Equal(original.Created, parsed.Created);
        Assert.Equal(original.Priority, parsed.Priority);
    }

    [Fact]
    public void Parse_RoundtripsDescription()
    {
        var task = MakeTask();
        task.Description = "Roundtrip this";
        var parsed = MarkdownSerializer.Parse(MarkdownSerializer.Serialize(task));
        Assert.Equal("Roundtrip this", parsed.Description);
    }

    [Fact]
    public void Parse_RoundtripsPlannedStart()
    {
        var task = MakeTask();
        task.PlannedStart = FixedDate;
        var parsed = MarkdownSerializer.Parse(MarkdownSerializer.Serialize(task));
        Assert.Equal(FixedDate, parsed.PlannedStart);
    }

    [Fact]
    public void Parse_RoundtripsPlannedEnd()
    {
        var task = MakeTask();
        task.PlannedEnd = FixedDate.AddDays(14);
        var parsed = MarkdownSerializer.Parse(MarkdownSerializer.Serialize(task));
        Assert.Equal(FixedDate.AddDays(14), parsed.PlannedEnd);
    }

    [Fact]
    public void Parse_PlannedStartIsNull_WhenAbsentFromFrontmatter()
    {
        const string text = "---\nid: x\ntitle: t\ngroupId: g\nboardId: b\ncreated: 2025-01-01T00:00:00Z\npriority: Medium\n---\n";
        var task = MarkdownSerializer.Parse(text);
        Assert.Null(task.PlannedStart);
    }

    [Fact]
    public void Parse_PlannedEndIsNull_WhenAbsentFromFrontmatter()
    {
        const string text = "---\nid: x\ntitle: t\ngroupId: g\nboardId: b\ncreated: 2025-01-01T00:00:00Z\npriority: Medium\n---\n";
        var task = MarkdownSerializer.Parse(text);
        Assert.Null(task.PlannedEnd);
    }

    [Fact]
    public void Parse_MultilineDescription_IsPreserved()
    {
        var task = MakeTask();
        task.Description = "line1\nline2\nline3";
        var parsed = MarkdownSerializer.Parse(MarkdownSerializer.Serialize(task));
        Assert.Equal("line1\nline2\nline3", parsed.Description);
    }

    [Fact]
    public void Parse_EmptyDescription_WhenNoBodyPresent()
    {
        const string text = "---\nid: x\ntitle: t\ngroupId: g\nboardId: b\ncreated: 2025-01-01T00:00:00Z\npriority: Medium\n---\n";
        var task = MarkdownSerializer.Parse(text);
        Assert.Equal(string.Empty, task.Description);
    }
}
