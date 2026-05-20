using System;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Corvida.Services;
using Corvida.ViewModels;
using Corvida.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Corvida;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            var services = new ServiceCollection();

            // Services
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IBoardService, BoardService>();
            services.AddSingleton<ITaskService, TaskService>();
            services.AddSingleton<IDialogService, DialogService>();

            // Pages
            services.AddTransient<PageBase, BoardsPageViewModel>();
            services.AddTransient<PageBase, SettingsViewModel>();

            // Register main view model
            services.AddSingleton<MainWindowViewModel>();

            Services = services.BuildServiceProvider();

            // Load settings before showing window
            Services.GetRequiredService<ISettingsService>().LoadAsync().GetAwaiter().GetResult();

            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        // BindingPlugins is internal in Avalonia 12, access via reflection
        var bindingPluginsType = typeof(DataAnnotationsValidationPlugin).Assembly
            
            .GetType("Avalonia.Data.Core.Plugins.BindingPlugins");
        var dataValidators = bindingPluginsType?
            .GetProperty("DataValidators", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null) as System.Collections.IList;
        if (dataValidators == null) return;
        for (var i = dataValidators.Count - 1; i >= 0; i--)
            if (dataValidators[i] is DataAnnotationsValidationPlugin)
                dataValidators.RemoveAt(i);
    }
}