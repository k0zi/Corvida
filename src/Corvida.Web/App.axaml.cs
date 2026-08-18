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
using SukiUI.Dialogs;
using System;
using System.Reflection;

namespace Corvida.Web;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

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

            services.AddSingleton<ISettingsService, StaticSettingsService>();
            services.AddSingleton<ISukiDialogManager, SukiDialogManager>();
            services.AddSingleton<IDialogService, DialogService>();

            services.AddSingleton<IBoardService, HttpBoardService>();
            services.AddSingleton<ITaskService, HttpTaskService>();
            services.AddSingleton<IAgentService, HttpAgentService>();
            services.AddSingleton<IRealtimeClient, SignalRRealtimeClient>();
            services.AddHttpClient("CorvidaApi");

            // Pages
            services.AddTransient<PageBase, BoardsPageViewModel>();
            services.AddTransient<PageBase, AgentsPageViewModel>();
            services.AddTransient<PageBase, ArchivedBoardsViewModel>();

            services.AddSingleton<MainWindowViewModel>();

            Services = services.BuildServiceProvider();

            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            var boardsPage = mainVm.Pages.OfType<BoardsPageViewModel>().First();
            var archivedPage = mainVm.Pages.OfType<ArchivedBoardsViewModel>().First();
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
