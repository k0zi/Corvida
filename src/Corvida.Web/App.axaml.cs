using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Corvida.Messages;
using Corvida.Services;
using Corvida.ViewModels;
using Corvida.Web.Services;
using Corvida.Web.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Corvida.Web;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Resolved by <see cref="Program"/> before the UI starts.</summary>
    public static string ApiBaseUrl { get; set; } = string.Empty;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            DisableAvaloniaDataAnnotationValidation();

            var services = new ServiceCollection();

            services.AddSingleton<ISettingsService>(new StaticSettingsService(ApiBaseUrl));
            services.AddSingleton<DialogManager>();
            services.AddSingleton<IDialogService, DialogService>();

            services.AddSingleton<IBoardService, HttpBoardService>();
            services.AddSingleton<ITaskService, HttpTaskService>();
            services.AddSingleton<IAgentService, HttpAgentService>();
            services.AddSingleton<IRealtimeClient, SignalRRealtimeClient>();

            // In-memory only: the browser build has no persistence, and a refresh token in
            // localStorage would be readable by any script that got injected into the page.
            // The cost is that reloading the tab signs the user out.
            services.AddSingleton<ITokenStore, InMemoryTokenStore>();
            services.AddSingleton<IAuthClient, AuthClient>();
            services.AddSingleton<ITokenProvider, TokenProvider>();
            services.AddTransient<AuthMessageHandler>();
            services.AddSingleton<IUserAdminService, HttpUserAdminService>();
            services.AddSingleton<IApiKeyService, HttpApiKeyService>();
            services.AddHttpClient("CorvidaApi").AddHttpMessageHandler<AuthMessageHandler>();

            // Pages
            services.AddTransient<PageBase, BoardsPageViewModel>();
            services.AddTransient<PageBase, AgentsPageViewModel>();
            services.AddTransient<PageBase, UsersPageViewModel>();
            services.AddTransient<PageBase, ArchivedBoardsViewModel>();

            services.AddSingleton<MainWindowViewModel>();

            Services = services.BuildServiceProvider();

            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            var boardsPage = mainVm.AllPages.OfType<BoardsPageViewModel>().First();
            var agentsPage = mainVm.AllPages.OfType<AgentsPageViewModel>().First();
            var archivedPage = mainVm.AllPages.OfType<ArchivedBoardsViewModel>().First();
            var usersPage = mainVm.AllPages.OfType<UsersPageViewModel>().First();
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

            async Task StartAuthenticatedWorkAsync()
            {
                await agentsPage.RefreshAsync();
                await realtime.RestartAsync();
                if (mainVm.CurrentUser?.IsAdmin == true) await usersPage.RefreshAsync();
                await boardsPage.RefreshAsync();
            }

            mainVm.SignedIn += StartAuthenticatedWorkAsync;
            mainVm.SignedOutOfServer += () => realtime.StopAsync();

            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await mainVm.InitializeAsync();
                if (mainVm.IsSignedIn) await StartAuthenticatedWorkAsync();
            });

            singleView.MainView = new MainView
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
