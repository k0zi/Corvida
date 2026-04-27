using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public class SettingsService : ISettingsService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Corvida", "settings.json");

    public AppSettings Settings { get; private set; } = new();

    public async Task LoadAsync()
    {
        if (!File.Exists(ConfigPath))
        {
            Settings = new AppSettings();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(ConfigPath);
            Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public async Task SaveAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(ConfigPath, json);
    }
}
