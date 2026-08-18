using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Corvida.Services;

public class SkillInstallerService : ISkillInstallerService
{
    public bool IsInstalled(string targetRoot)
    {
        var root = ExpandPath(targetRoot);
        foreach (var skillName in SkillNames())
        {
            if (!File.Exists(Path.Combine(root, skillName, "SKILL.md")))
                return false;
        }
        return true;
    }

    public async Task InstallAsync(string targetRoot)
    {
        var root = ExpandPath(targetRoot);
        foreach (var skillName in SkillNames())
        {
            var sourceDir = Path.Combine(SkillPaths.UserSkillsRoot, skillName);
            var destDir = Path.Combine(root, skillName);
            Directory.CreateDirectory(destDir);

            await CopyFileAsync(Path.Combine(sourceDir, "SKILL.md"), Path.Combine(destDir, "SKILL.md"));

            var sourceAgentsDir = Path.Combine(sourceDir, "agents");
            if (Directory.Exists(sourceAgentsDir))
                SkillPaths.CopyDirectory(sourceAgentsDir, Path.Combine(destDir, "agents"));
        }
    }

    public Task UninstallAsync(string targetRoot)
    {
        var root = ExpandPath(targetRoot);
        foreach (var skillName in SkillNames())
        {
            var destDir = Path.Combine(root, skillName);
            if (Directory.Exists(destDir))
                Directory.Delete(destDir, recursive: true);
        }
        return Task.CompletedTask;
    }

    private static string[] SkillNames()
    {
        SkillPaths.EnsureSeeded();
        return Directory.GetDirectories(SkillPaths.UserSkillsRoot)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToArray();
    }

    private static async Task CopyFileAsync(string sourceFile, string destFile)
    {
        await using var source = File.OpenRead(sourceFile);
        await using var dest = File.Create(destFile);
        await source.CopyToAsync(dest);
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
