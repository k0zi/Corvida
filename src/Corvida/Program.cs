using Avalonia;
using KD.Avalonia.Rice;
using KD.Avalonia.Rice.Options;
using KD.Avalonia.Rice.Theming;
using System;

namespace Corvida;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseRice(options =>
            {
                // AppId is the settings folder, so the chosen theme lands next to settings.json.
                options.AppInfo = new RiceAppInfo
                {
                    AppId = "Corvida",
                    Name = "Corvida",
                    Description = "Kanban boards for people and AI agents.",
                    IconUri = "avares://Corvida/Assets/corvida.png",
                };
                options.DefaultThemeId = BuiltInThemes.Windows11.Id;
            })
            .LogToTrace();
}