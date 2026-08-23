using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

public class AgentSlugTests
{
    private static Agent Named(string name, string id = "agent-1") => new() { Id = id, Name = name };

    [Theory]
    [InlineData("Architect", "architect")]
    [InlineData("Code-Reviewer", "code-reviewer")]
    [InlineData("Security Reviewer", "security-reviewer")]
    [InlineData("Agent 3", "agent-3")]
    [InlineData("  Trailing punctuation!  ", "trailing-punctuation")]
    public void For_SlugifiesPlainNames(string name, string expected) =>
        Assert.Equal(expected, AgentSlug.For(Named(name)));

    // A plain [^a-z0-9] filter turns "Tervező" into "tervez" and "Árvíztűrő" into "rv-zt-r";
    // folding the accents first keeps the name recognisable.
    [Theory]
    [InlineData("Tervező", "tervezo")]
    [InlineData("Árvíztűrő", "arvizturo")]
    [InlineData("Kovács Béla", "kovacs-bela")]
    public void For_FoldsAccentsRatherThanDroppingThem(string name, string expected) =>
        Assert.Equal(expected, AgentSlug.For(Named(name)));

    [Fact]
    public void For_FallsBackToTheId_WhenTheNameLeavesNothingBehind()
    {
        // Without a fallback this agent was written to a file literally called ".md".
        var slug = AgentSlug.For(Named("Дэвид", id: "builtin-architect"));
        Assert.Equal("builtin-architect", slug);
    }

    [Fact]
    public void For_FallsBackToAHash_WhenNeitherNameNorIdSurvives()
    {
        var slug = AgentSlug.For(Named("Дэвид", id: "Дэвид"));
        Assert.StartsWith("agent-", slug);
        Assert.True(slug.Length > "agent-".Length);
    }

    [Fact]
    public void For_IsStableAcrossCalls() =>
        Assert.Equal(AgentSlug.For(Named("Дэвид", id: "x")), AgentSlug.For(Named("Дэвид", id: "x")));

    [Fact]
    public void Disambiguate_ProducesDifferentSlugsForDifferentAgents()
    {
        var first = AgentSlug.Disambiguate("dev-team", Named("Dev Team", id: "a"));
        var second = AgentSlug.Disambiguate("dev-team", Named("dev team", id: "b"));

        Assert.NotEqual(first, second);
        Assert.StartsWith("dev-team-", first);
    }
}
