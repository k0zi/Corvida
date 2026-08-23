using System;
using System.Globalization;
using System.Text;

namespace Corvida.Services;

/// <summary>
/// Emits and reads the single-line scalars used by Corvida's YAML frontmatter blocks.
/// </summary>
/// <remarks>
/// The frontmatter writers used to interpolate values straight into <c>key: {value}</c>, which
/// breaks on any value a user can actually type: the agent Description box accepts newlines, and a
/// description containing <c>": "</c> is an invalid YAML mapping. Both corrupt the file on the next
/// read — and, for exported subagents, make the consuming tool skip the agent entirely.
/// This is deliberately not a general YAML implementation; it only has to survive round-tripping
/// arbitrary user text through a flow scalar.
/// </remarks>
public static class YamlScalar
{
    /// <summary>Renders <paramref name="value"/> as a scalar that is safe on one line.</summary>
    public static string Encode(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return NeedsQuoting(value) ? Quote(value) : value;
    }

    /// <summary>Reads a scalar produced by <see cref="Encode"/> (or a hand-written plain one).</summary>
    public static string Decode(string raw)
    {
        var value = raw.Trim();
        if (value.Length < 2) return value;

        if (value[0] == '"' && value[^1] == '"')
            return Unescape(value[1..^1]);

        if (value[0] == '\'' && value[^1] == '\'')
            return value[1..^1].Replace("''", "'");

        return value;
    }

    /// <summary>Collapses newlines to spaces, for keys that must stay a single readable line.</summary>
    public static string Flatten(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var sb = new StringBuilder(value.Length);
        var lastWasSpace = false;
        foreach (var c in value)
        {
            var isSpace = c is '\r' or '\n' or '\t' || char.IsWhiteSpace(c);
            if (isSpace)
            {
                if (!lastWasSpace && sb.Length > 0) sb.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }
        return sb.ToString().TrimEnd();
    }

    private static bool NeedsQuoting(string value)
    {
        if (value != value.Trim()) return true;
        if (value.Contains(": ", StringComparison.Ordinal)) return true;
        if (value.EndsWith(':')) return true;
        if (value.Contains(" #", StringComparison.Ordinal)) return true;

        foreach (var c in value)
            if (c is '\n' or '\r' or '\t' or '"' or '\'' or '\\') return true;

        // Leading indicators change how a plain scalar is parsed (or make it invalid outright).
        if ("-?:,[]{}#&*!|>%@`".IndexOf(value[0]) >= 0) return true;

        // Values a YAML reader would hand back as something other than a string.
        if (value is "true" or "false" or "null" or "~" or "yes" or "no" or "on" or "off") return true;
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) return true;

        return false;
    }

    private static string Quote(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static string Unescape(string value)
    {
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                sb.Append(value[i]);
                continue;
            }

            i++;
            switch (value[i])
            {
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                default: sb.Append('\\').Append(value[i]); break;
            }
        }
        return sb.ToString();
    }
}
