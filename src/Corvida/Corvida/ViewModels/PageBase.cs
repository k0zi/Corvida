using Material.Icons;

namespace Corvida.ViewModels;

public abstract class PageBase : ViewModelBase
{
    public abstract string MenuTitle { get; }
    public abstract MaterialIconKind Icon { get; }
    public virtual int DisplayOrder => 0;
}