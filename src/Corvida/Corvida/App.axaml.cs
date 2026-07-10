using System;
using System.Linq;
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
            services.AddSingleton<IDialogService, DialogService>();

            // Storage: concrete implementations + strategy adapters
            services.AddSingleton<BoardService>();
            services.AddSingleton<HttpBoardService>();
            services.AddSingleton<IBoardService, StorageAwareBoardService>();

            services.AddSingleton<TaskService>();
            services.AddSingleton<HttpTaskService>();
            services.AddSingleton<ITaskService, StorageAwareTaskService>();

            services.AddSingleton<IExportService, ExportService>();
            services.AddHttpClient("CorvidaApi");

            // Pages
            services.AddTransient<PageBase, BoardsPageViewModel>();
            services.AddTransient<PageBase, SettingsViewModel>();

            // Register main view model
            services.AddSingleton<MainWindowViewModel>();

            Services = services.BuildServiceProvider();

            // Load settings before showing window
            Services.GetRequiredService<ISettingsService>().LoadAsync().GetAwaiter().GetResult();

            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            var boardsPage = mainVm.Pages.OfType<BoardsPageViewModel>().First();
            var settingsPage = mainVm.Pages.OfType<SettingsViewModel>().First();
            settingsPage.SetOnSaved(async () =>
            {
                mainVm.ActivePage = boardsPage;
                await boardsPage.RefreshAsync();
            });

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
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