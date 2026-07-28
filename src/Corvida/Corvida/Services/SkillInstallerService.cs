using System;
using System.IO;
using System.Threading.Tasks;

namespace Corvida.Services;

public class SkillInstallerService : ISkillInstallerService
{
    private static readonly string[] SkillNames = ["corvida-implement", "corvida-plan"];

    public bool IsInstalled(string targetRoot)
    {
        var root = ExpandPath(targetRoot);
        foreach (var skillName in SkillNames)
        {
            if (!File.Exists(Path.Combine(root, skillName, "SKILL.md")))
                return false;
        }
        return true;
    }

    public async Task InstallAsync(string targetRoot)
    {
        var root = ExpandPath(targetRoot);
        foreach (var skillName in SkillNames)
        {
            var sourceFile = Path.Combine(AppContext.BaseDirectory, "Skills", skillName, "SKILL.md");
            var destDir = Path.Combine(root, skillName);
            Directory.CreateDirectory(destDir);
            await using var source = File.OpenRead(sourceFile);
            await using var dest = File.Create(Path.Combine(destDir, "SKILL.md"));
            await source.CopyToAsync(dest);
        }
    }

    public Task UninstallAsync(string targetRoot)
    {
        var root = ExpandPath(targetRoot);
        foreach (var skillName in SkillNames)
        {
            var destDir = Path.Combine(root, skillName);
            if (Directory.Exists(destDir))
                Directory.Delete(destDir, recursive: true);
        }
        return Task.CompletedTask;
    }

    private static string ExpandPath(string path)
    {
        path = path.Trim();
        if (path.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = home + path[1..];
        }
        return path;
    }
}
