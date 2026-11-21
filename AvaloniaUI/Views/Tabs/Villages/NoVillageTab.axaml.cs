using MainCore.UI.ViewModels.Tabs.Villages;

using ReactiveUI;
using ReactiveUI.Avalonia;

namespace AvaloniaUI.Views.Tabs.Villages
{
    public class NoVillageTabBase : ReactiveUserControl<NoVillageViewModel>
    {
    }

    /// <summary>
    /// Code-behind for NoVillageTAb.axaml
    /// </summary>
    public partial class NoVillageTab : NoVillageTabBase
    {
        public NoVillageTab()
        {
            InitializeComponent();
        }
    }
}