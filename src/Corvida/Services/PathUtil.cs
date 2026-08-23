using System;

namespace Corvida.Services;

public static class PathUtil
{
    /// <summary>Resolves the <c>~</c>-prefixed paths the Settings page's target fields accept.</summary>
    public static string Expand(string path)
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
