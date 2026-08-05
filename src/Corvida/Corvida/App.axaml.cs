using System;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Corvida.Messages;
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

            services.AddSingleton<AgentService>();
            services.AddSingleton<HttpAgentService>();
            services.AddSingleton<IAgentService, StorageAwareAgentService>();

            services.AddSingleton<IExportService, ExportService>();
            services.AddSingleton<ISkillInstallerService, SkillInstallerService>();
            services.AddSingleton<ISkillService, SkillService>();
            services.AddSingleton<IRealtimeClient, SignalRRealtimeClient>();
            services.AddHttpClient("CorvidaApi");

            // Pages
            services.AddTransient<PageBase, BoardsPageViewModel>();
            services.AddTransient<PageBase, AgentsPageViewModel>();
            services.AddTransient<PageBase, SkillsPageViewModel>();
            services.AddTransient<PageBase, ArchivedBoardsViewModel>();
            services.AddTransient<PageBase, SettingsViewModel>();

            // Register main view model
            services.AddSingleton<MainWindowViewModel>();

            Services = services.BuildServiceProvider();

            // Load settings before showing window
            Services.GetRequiredService<ISettingsService>().LoadAsync().GetAwaiter().GetResult();
            SkillPaths.EnsureSeeded();

            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            var boardsPage = mainVm.Pages.OfType<BoardsPageViewModel>().First();
            var archivedPage = mainVm.Pages.OfType<ArchivedBoardsViewModel>().First();
            var settingsPage = mainVm.Pages.OfType<SettingsViewModel>().First();
            settingsPage.SetOnSaved(async () =>
            {
                mainVm.ActivePage = boardsPage;
                await boardsPage.RefreshAsync();
            });
            archivedPage.SetOnViewBoard(board =>
            {
                mainVm.ActivePage = boardsPage;
                boardsPage.NavigateToBoardEditor(board);
            });

            var realtime = Services.GetRequiredService<IRealtimeClient>();
            realtime.BoardChanged += board =>
                Dispatcher.UIThread.Post(() => WeakReferenceMessenger.Default.Send(new BoardChangedMessage(board)));
            realtime.BoardDeleted += boardId =>
                Dispatcher.UIThread.Post(() => WeakReferenceMessenger.Default.Send(new BoardDeletedMessage(boardId)));
            realtime.TaskChanged += (boardId, task) =>
                Dispatcher.UIThread.Post(() => WeakReferenceMessenger.Default.Send(new TaskChangedMessage(boardId, task)));
            realtime.TaskDeleted += (boardId, taskId) =>
                Dispatcher.UIThread.Post(() => WeakReferenceMessenger.Default.Send(new TaskDeletedMessage(boardId, taskId)));
            realtime.AgentChanged += agent =>
                Dispatcher.UIThread.Post(() => WeakReferenceMessenger.Default.Send(new AgentChangedMessage(agent)));
            realtime.AgentDeleted += agentId =>
                Dispatcher.UIThread.Post(() => WeakReferenceMessenger.Default.Send(new AgentDeletedMessage(agentId)));
            _ = realtime.StartAsync();
            desktop.ShutdownRequested += (_, _) => { _ = realtime.StopAsync(); };

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