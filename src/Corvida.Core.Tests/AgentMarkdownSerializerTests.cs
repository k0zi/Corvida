using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

public class AgentMarkdownSerializerTests
{
    private static Agent MakeAgent() => new()
    {
        Id = "agent-123",
        Name = "Ada",
        Color = "#4C6EF5",
        Personality = "Curious and precise."
    };

    [Fact]
    public void Serialize_OutputStartsWithFrontmatterDelimiter()
    {
        var output = AgentMarkdownSerializer.Serialize(MakeAgent());
        Assert.StartsWith("---", output);
    }

    [Fact]
    public void Serialize_FrontmatterContainsId()
    {
        var agent = MakeAgent();
        var output = AgentMarkdownSerializer.Serialize(agent);
        Assert.Contains($"id: {agent.Id}", output);
    }

    [Fact]
    public void Serialize_FrontmatterContainsName()
    {
        var agent = MakeAgent();
        var output = AgentMarkdownSerializer.Serialize(agent);
        Assert.Contains($"name: {agent.Name}", output);
    }

    // A hex colour has to be quoted: unquoted, YAML reads the leading '#' as a comment and the
    // whole value disappears. Corvida's own hand parser tolerated it, a real one would not.
    [Fact]
    public void Serialize_QuotesHexColor()
    {
        var agent = MakeAgent();
        var output = AgentMarkdownSerializer.Serialize(agent);
        Assert.Contains($"color: \"{agent.Color}\"", output);
    }

    [Fact]
    public void Parse_ReadsUnquotedColor_FromFilesWrittenBeforeQuotingWasAdded()
    {
        var agent = AgentMarkdownSerializer.Parse("---\nid: x\nname: n\ncolor: #4C6EF5\n---\n");
        Assert.Equal("#4C6EF5", agent.Color);
    }

    [Fact]
    public void Parse_RoundtripsDescriptionContainingAColon()
    {
        var agent = MakeAgent();
        agent.Description = "Use when: the build breaks.";
        var parsed = AgentMarkdownSerializer.Parse(AgentMarkdownSerializer.Serialize(agent));
        Assert.Equal(agent.Description, parsed.Description);
    }

    [Fact]
    public void Parse_RoundtripsMultilineDescription()
    {
        var agent = MakeAgent();
        agent.Description = "First line.\nSecond line.";
        var parsed = AgentMarkdownSerializer.Parse(AgentMarkdownSerializer.Serialize(agent));
        Assert.Equal(agent.Description, parsed.Description);
    }

    // A newline in the description used to spill the rest of the value into the frontmatter as a
    // stray line, which silently truncated the description and corrupted every key after it.
    [Fact]
    public void Serialize_MultilineDescriptionStaysOnOneFrontmatterLine()
    {
        var agent = MakeAgent();
        agent.Description = "First line.\nSecond line.";
        var frontmatter = AgentMarkdownSerializer.Serialize(agent).Split("---")[1];
        Assert.DoesNotContain("Second line.\n", frontmatter);
        Assert.Single(frontmatter.Split('\n'), l => l.StartsWith("description:"));
    }

    [Fact]
    public void Parse_RoundtripsNameContainingAColon()
    {
        var agent = MakeAgent();
        agent.Name = "Ada: the reviewer";
        var parsed = AgentMarkdownSerializer.Parse(AgentMarkdownSerializer.Serialize(agent));
        Assert.Equal(agent.Name, parsed.Name);
    }

    [Fact]
    public void Serialize_FrontmatterContainsDescription()
    {
        var agent = MakeAgent();
        agent.Description = "Use when reviewing pull requests.";
        var output = AgentMarkdownSerializer.Serialize(agent);
        Assert.Contains($"description: {agent.Description}", output);
    }

    [Fact]
    public void Serialize_OmitsDescriptionWhenEmpty()
    {
        var agent = MakeAgent();
        agent.Description = string.Empty;
        Assert.DoesNotContain("description:", AgentMarkdownSerializer.Serialize(agent));
    }

    [Fact]
    public void Parse_RoundtripsDescription()
    {
        var agent = MakeAgent();
        agent.Description = "Use when reviewing pull requests.";
        var parsed = AgentMarkdownSerializer.Parse(AgentMarkdownSerializer.Serialize(agent));
        Assert.Equal(agent.Description, parsed.Description);
    }

    [Fact]
    public void Serialize_OmitsAvatarWhenNull()
    {
        var agent = MakeAgent();
        agent.AvatarDataUri = null;
        Assert.DoesNotContain("avatar:", AgentMarkdownSerializer.Serialize(agent));
    }

    [Fact]
    public void Serialize_IncludesAvatarWhenSet()
    {
        var agent = MakeAgent();
        agent.AvatarDataUri = "data:image/png;base64,AAAA";
        Assert.Contains($"avatar: {agent.AvatarDataUri}", AgentMarkdownSerializer.Serialize(agent));
    }

    [Fact]
    public void Serialize_PersonalityAppearsAfterFrontmatter()
    {
        var agent = MakeAgent();
        agent.Personality = "My personality";
        var output = AgentMarkdownSerializer.Serialize(agent);

        var firstDelim = output.IndexOf("---", StringComparison.Ordinal);
        var secondDelim = output.IndexOf("---", firstDelim + 3, StringComparison.Ordinal);
        var pos = output.IndexOf("My personality", StringComparison.Ordinal);

        Assert.True(secondDelim >= 0);
        Assert.True(pos > secondDelim);
    }

    [Fact]
    public void Parse_RoundtripsAllRequiredFields()
    {
        var original = MakeAgent();
        var parsed = AgentMarkdownSerializer.Parse(AgentMarkdownSerializer.Serialize(original));

        Assert.Equal(original.Id, parsed.Id);
        Assert.Equal(original.Name, parsed.Name);
        Assert.Equal(original.Color, parsed.Color);
    }

    [Fact]
    public void Parse_RoundtripsAvatar()
    {
        var agent = MakeAgent();
        agent.AvatarDataUri = "data:image/png;base64,AAAA";
        var parsed = AgentMarkdownSerializer.Parse(AgentMarkdownSerializer.Serialize(agent));
        Assert.Equal(agent.AvatarDataUri, parsed.AvatarDataUri);
    }

    [Fact]
    public void Parse_AvatarIsNull_WhenAbsentFromFrontmatter()
    {
        const string text = "---\nid: x\nname: n\ncolor: #000000\n---\n";
        var agent = AgentMarkdownSerializer.Parse(text);
        Assert.Null(agent.AvatarDataUri);
    }

    [Fact]
    public void Parse_RoundtripsMultilinePersonality()
    {
        var agent = MakeAgent();
        agent.Personality = "line1\nline2\nline3";
        var parsed = AgentMarkdownSerializer.Parse(AgentMarkdownSerializer.Serialize(agent));
        Assert.Equal("line1\nline2\nline3", parsed.Personality);
    }

    [Fact]
    public void Parse_EmptyPersonality_WhenNoBodyPresent()
    {
        const string text = "---\nid: x\nname: n\ncolor: #000000\n---\n";
        var agent = AgentMarkdownSerializer.Parse(text);
        Assert.Equal(string.Empty, agent.Personality);
    }
}
