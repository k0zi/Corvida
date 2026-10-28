using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
            services.AddSingleton<DialogManager>();
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
            services.AddSingleton<IAgentInstallerService, AgentInstallerService>();
            services.AddSingleton<IMcpInstallerService, McpInstallerService>();
            services.AddSingleton<IRealtimeClient, SignalRRealtimeClient>();

            // Authentication. The token lives in memory only, so closing the app signs out.
            services.AddSingleton<ITokenStore, InMemoryTokenStore>();
            services.AddSingleton<IAuthClient, AuthClient>();
            services.AddSingleton<ITokenProvider, TokenProvider>();
            services.AddTransient<AuthMessageHandler>();

            // Registering the handler here covers every Http*Service at once: each resolves its
            // HttpClient from the factory per call, so none of them needed changing.
            services.AddSingleton<IUserAdminService, HttpUserAdminService>();
            services.AddSingleton<IApiKeyService, HttpApiKeyService>();
            services.AddHttpClient("CorvidaApi").AddHttpMessageHandler<AuthMessageHandler>();

            // Pages
            services.AddTransient<PageBase, BoardsPageViewModel>();
            services.AddTransient<PageBase, AgentsPageViewModel>();
            services.AddTransient<PageBase, SkillsPageViewModel>();
            services.AddTransient<PageBase, ArchivedBoardsViewModel>();
            services.AddTransient<PageBase, UsersPageViewModel>();
            services.AddTransient<PageBase, SettingsViewModel>();

            // Register main view model
            services.AddSingleton<MainWindowViewModel>();

            Services = services.BuildServiceProvider();

            // Load settings before showing window
            Services.GetRequiredService<ISettingsService>().LoadAsync().GetAwaiter().GetResult();
            SkillPaths.EnsureSeeded();

            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            var boardsPage = mainVm.AllPages.OfType<BoardsPageViewModel>().First();
            var agentsPage = mainVm.AllPages.OfType<AgentsPageViewModel>().First();
            var archivedPage = mainVm.AllPages.OfType<ArchivedBoardsViewModel>().First();
            var usersPage = mainVm.AllPages.OfType<UsersPageViewModel>().First();
            var settingsPage = mainVm.AllPages.OfType<SettingsViewModel>().First();
            settingsPage.SetOnSaved(async () =>
            {
                mainVm.ActivePage = boardsPage;
                await settingsPage.RefreshAuthStateAsync();
                await boardsPage.RefreshAsync();
            });

            // Turning the switch on or off from Settings changes what the whole shell should show,
            // so re-run the same gate check the app does at startup.
            settingsPage.SetOnAuthChanged(async () =>
            {
                await settingsPage.RefreshAuthStateAsync();
                await mainVm.InitializeAsync();
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
            realtime.AuthStateChanged += required =>
                Dispatcher.UIThread.Post(() => _ = mainVm.OnAuthStateChangedAsync(required));

            // Seeding and the realtime connection both call the API, so they must wait until we
            // know whether a token is needed — behind a closed gate they would 401, and the
            // seeder in particular would swallow that and never retry.
            async Task StartAuthenticatedWorkAsync()
            {
                try
                {
                    await BuiltInAgentSeeder.EnsureSeededAsync(Services.GetRequiredService<IAgentService>());
                }
                catch
                {
                    // Best-effort default content; the user can still create agents manually.
                }

                await agentsPage.RefreshAsync();
                await realtime.RestartAsync();
                if (mainVm.CurrentUser?.IsAdmin == true) await usersPage.RefreshAsync();
                await settingsPage.RefreshAuthStateAsync();
                await boardsPage.RefreshAsync();
            }

            mainVm.SignedIn += StartAuthenticatedWorkAsync;
            mainVm.SignedOutOfServer += () => realtime.StopAsync();

            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await mainVm.InitializeAsync();

                // Already through the gate (auth is off, or local storage): do the startup work now.
                if (mainVm.IsSignedIn) await StartAuthenticatedWorkAsync();
            });

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