using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Corvida.ViewModels;

namespace Corvida.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UserNameBox.Focus();
    }

    /// <summary>Enter submits from either field, which is what a two-field login form should do.</summary>
    private void Field_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is not LoginViewModel vm) return;

        if (vm.SignInCommand.CanExecute(null))
            vm.SignInCommand.Execute(null);

        e.Handled = true;
    }
}
