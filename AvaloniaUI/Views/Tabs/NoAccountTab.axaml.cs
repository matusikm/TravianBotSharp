using MainCore.UI.ViewModels.Tabs;

using ReactiveUI;
using ReactiveUI.Avalonia;

namespace AvaloniaUI.Views.Tabs
{
    public class NoAccountTabBase : ReactiveUserControl<NoAccountViewModel>
    {
    }

    /// <summary>
    /// Code-behind for NoAccountTab.axaml
    /// </summary>
    public partial class NoAccountTab : NoAccountTabBase
    {
        public NoAccountTab()
        {
            InitializeComponent();
        }
    }
}