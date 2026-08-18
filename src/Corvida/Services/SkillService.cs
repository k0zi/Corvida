using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public class SkillService : ISkillService
{
    public async Task<List<Skill>> GetSkillsAsync()
    {
        SkillPaths.EnsureSeeded();

        var skills = new List<Skill>();
        foreach (var dir in Directory.GetDirectories(SkillPaths.UserSkillsRoot))
        {
            var id = Path.GetFileName(dir);
            var skillMdPath = Path.Combine(dir, "SKILL.md");
            if (!File.Exists(skillMdPath)) continue;

            var (name, description, body) = SkillMarkdownSerializer.Parse(
                await File.ReadAllTextAsync(skillMdPath));

            var displayName = string.Empty;
            var shortDescription = string.Empty;
            var yamlPath = Path.Combine(dir, "agents", "openai.yaml");
            if (File.Exists(yamlPath))
                (displayName, shortDescription) = SkillAgentYamlSerializer.Parse(
                    await File.ReadAllTextAsync(yamlPath));

            skills.Add(new Skill
            {
                Id = id,
                Name = name,
                Description = description,
                Body = body,
                DisplayName = displayName,
                ShortDescription = shortDescription,
            });
        }

        return skills.OrderBy(s => s.Name).ToList();
    }

    public async Task<Skill> CreateSkillAsync(string name)
    {
        SkillPaths.EnsureSeeded();

        var id = UniqueSlug(name.Trim());
        var skill = new Skill
        {
            Id = id,
            Name = name.Trim(),
            Description = "TODO: describe what this skill does and when to use it.",
            Body = $"# {name.Trim()}\n\nTODO: describe the skill's workflow.",
            DisplayName = name.Trim(),
            ShortDescription = "TODO",
        };

        await SaveSkillAsync(skill);
        return skill;
    }

    public async Task SaveSkillAsync(Skill skill)
    {
        SkillPaths.EnsureSeeded();

        var dir = Path.Combine(SkillPaths.UserSkillsRoot, skill.Id);
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "SKILL.md"),
            SkillMarkdownSerializer.Serialize(skill.Name, skill.Description, skill.Body));

        var agentsDir = Path.Combine(dir, "agents");
        Directory.CreateDirectory(agentsDir);
        await File.WriteAllTextAsync(Path.Combine(agentsDir, "openai.yaml"),
            SkillAgentYamlSerializer.Serialize(skill.DisplayName, skill.ShortDescription));
    }

    public Task DeleteSkillAsync(string id)
    {
        var dir = Path.Combine(SkillPaths.UserSkillsRoot, id);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
        return Task.CompletedTask;
    }

    private static string UniqueSlug(string name)
    {
        var baseSlug = Slugify(name);
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "skill";

        var slug = baseSlug;
        var suffix = 2;
        while (Directory.Exists(Path.Combine(SkillPaths.UserSkillsRoot, slug)))
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }
        return slug;
    }

    private static string Slugify(string value)
    {
        var lowered = value.ToLowerInvariant();
        var replaced = Regex.Replace(lowered, "[^a-z0-9]+", "-");
        return replaced.Trim('-');
    }
}
