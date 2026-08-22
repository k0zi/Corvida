using Material.Icons;

namespace Corvida.ViewModels;

public abstract class PageBase : ViewModelBase
{
    public abstract string MenuTitle { get; }
    public abstract MaterialIconKind Icon { get; }
    public virtual int DisplayOrder => 0;
}

/// <summary>
/// Marks a page that only an administrator may open, so the shell can keep it out of the menu.
/// </summary>
public interface IAdminOnlyPage;
