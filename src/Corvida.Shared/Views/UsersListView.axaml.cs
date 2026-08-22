using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Corvida.Views;

public partial class UsersListView : UserControl
{
    public UsersListView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
