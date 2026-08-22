using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Corvida.Views;

public partial class UsersPageView : UserControl
{
    public UsersPageView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
