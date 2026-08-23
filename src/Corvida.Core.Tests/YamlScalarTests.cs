using Corvida.Services;

namespace Corvida.Core.Tests;

public class YamlScalarTests
{
    [Theory]
    [InlineData("Ada")]
    [InlineData("Use when reviewing pull requests.")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("builtin-architect")]
    public void Encode_LeavesUnambiguousValuesPlain(string value) =>
        Assert.Equal(value, YamlScalar.Encode(value));

    [Theory]
    [InlineData("#4C6EF5")]            // '#' opens a comment
    [InlineData("Use when: it breaks")] // ": " starts a nested mapping
    [InlineData("- not a list item")]   // '-' opens a sequence entry
    [InlineData("true")]                // would come back as a boolean
    [InlineData("42")]                  // would come back as a number
    [InlineData(" padded ")]            // padding is otherwise lost
    [InlineData("trailing colon:")]
    [InlineData("hash # comment")]
    public void Encode_QuotesValuesThatWouldChangeMeaning(string value) =>
        Assert.StartsWith("\"", YamlScalar.Encode(value));

    [Theory]
    [InlineData("Ada")]
    [InlineData("#4C6EF5")]
    [InlineData("Use when: it breaks")]
    [InlineData("line one\nline two")]
    [InlineData("tab\there")]
    [InlineData("quote \" and backslash \\")]
    [InlineData("'single quoted'")]
    [InlineData("true")]
    [InlineData(" padded ")]
    public void EncodeThenDecode_Roundtrips(string value) =>
        Assert.Equal(value, YamlScalar.Decode(YamlScalar.Encode(value)));

    [Fact]
    public void Encode_KeepsMultilineValuesOnOneLine() =>
        Assert.DoesNotContain("\n", YamlScalar.Encode("line one\nline two"));

    [Fact]
    public void Encode_EmptyBecomesAnExplicitEmptyString() =>
        Assert.Equal("\"\"", YamlScalar.Encode(""));

    [Fact]
    public void Decode_ReadsPlainScalarsWrittenByHand() =>
        Assert.Equal("#4C6EF5", YamlScalar.Decode(" #4C6EF5 "));

    [Fact]
    public void Decode_ReadsSingleQuotedScalars() =>
        Assert.Equal("it's here", YamlScalar.Decode("'it''s here'"));

    [Fact]
    public void Flatten_CollapsesNewlinesAndRuns() =>
        Assert.Equal("First line. Second line.", YamlScalar.Flatten("First line.\n\n  Second line."));

    [Fact]
    public void Flatten_EmptyStaysEmpty() =>
        Assert.Equal("", YamlScalar.Flatten(null));
}
