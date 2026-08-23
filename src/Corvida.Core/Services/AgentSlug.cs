using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Corvida.Models;

namespace Corvida.Services;

/// <summary>
/// Turns an <see cref="Agent"/> name into the file name its exported definition is written under.
/// </summary>
/// <remarks>
/// Every target tool derives the agent's identity from this name, so it has to stay stable, unique
/// within one install, and inside the character set those tools accept (lowercase letters, digits
/// and hyphens).
/// </remarks>
public static class AgentSlug
{
    /// <summary>
    /// Slugifies <paramref name="agent"/>'s name, falling back to its id when the name carries no
    /// characters that survive transliteration.
    /// </summary>
    /// <remarks>
    /// Accents are folded rather than dropped, so "Tervező" becomes <c>tervezo</c> instead of the
    /// <c>tervez</c> a plain <c>[^a-z0-9]</c> filter produces. A name written entirely outside the
    /// Latin script still folds away to nothing, which is why the id (and then a hash of it) backs
    /// the name up — otherwise the agent was written to a file literally called ".md".
    /// </remarks>
    public static string For(Agent agent)
    {
        var fromName = Slugify(agent.Name);
        if (fromName.Length > 0) return fromName;

        var fromId = Slugify(agent.Id);
        if (fromId.Length > 0) return fromId;

        return "agent-" + ShortHash(agent.Id);
    }

    /// <summary>Appends a stable suffix so two agents slugifying alike get distinct files.</summary>
    public static string Disambiguate(string slug, Agent agent) => $"{slug}-{ShortHash(agent.Id)}";

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                folded.Append(c);

        var lowered = folded.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        return Regex.Replace(lowered, "[^a-z0-9]+", "-").Trim('-');
    }

    private static string ShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexStringLower(bytes)[..8];
    }
}
