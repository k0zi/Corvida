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

    [Fact]
    public void Serialize_FrontmatterContainsColor()
    {
        var agent = MakeAgent();
        var output = AgentMarkdownSerializer.Serialize(agent);
        Assert.Contains($"color: {agent.Color}", output);
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
