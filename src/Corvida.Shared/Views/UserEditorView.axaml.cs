using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Corvida.Views;

public partial class UserEditorView : UserControl
{
    public UserEditorView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
