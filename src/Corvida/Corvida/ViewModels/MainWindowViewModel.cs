using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using SukiUI;

namespace Corvida.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<PageBase> Pages { get; }

    [ObservableProperty] private MaterialIconKind _themeIcon = MaterialIconKind.WeatherSunny;

    public MainWindowViewModel(IEnumerable<PageBase> pages)
    {
        Pages = new ObservableCollection<PageBase>(pages
            .OrderBy(p => p.DisplayOrder));

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