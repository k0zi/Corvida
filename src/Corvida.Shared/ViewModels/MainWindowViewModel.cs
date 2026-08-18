using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using SukiUI;
using SukiUI.Dialogs;

namespace Corvida.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<PageBase> Pages { get; }
    public ISukiDialogManager DialogManager { get; }

    [ObservableProperty] private MaterialIconKind _themeIcon = MaterialIconKind.WeatherSunny;
    [ObservableProperty] private PageBase? _activePage;

    public MainWindowViewModel(IEnumerable<PageBase> pages, ISukiDialogManager dialogManager)
    {
        Pages = new ObservableCollection<PageBase>(pages
            .OrderBy(p => p.DisplayOrder));
        DialogManager = dialogManager;

        var theme = SukiTheme.GetInstance();
        UpdateThemeIcon(theme.ActiveBaseTheme);
        theme.OnBaseThemeChanged += variant => UpdateThemeIcon(variant);
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        SukiTheme.GetInstance().SwitchBaseTheme();
    }

    private void UpdateThemeIcon(ThemeVariant variant)
    {
        ThemeIcon = variant == ThemeVariant.Dark
            ? MaterialIconKind.LightbulbVariant
            : MaterialIconKind.LightbulbOnOutline;
    }
}