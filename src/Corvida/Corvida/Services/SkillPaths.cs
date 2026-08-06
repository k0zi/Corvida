using System;
using System.IO;

namespace Corvida.Services;

public static class SkillPaths
{
    public static string UserSkillsRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Corvida", "skills");

    public static string BundledSkillsRoot => Path.Combine(AppContext.BaseDirectory, "Skills");

    public static void EnsureSeeded()
    {
        if (Directory.Exists(UserSkillsRoot)) return;

        Directory.CreateDirectory(UserSkillsRoot);
        if (Directory.Exists(BundledSkillsRoot))
            CopyDirectory(BundledSkillsRoot, UserSkillsRoot);
    }

    internal static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);

        foreach (var subDir in Directory.GetDirectories(sourceDir))
            CopyDirectory(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
    }
}
