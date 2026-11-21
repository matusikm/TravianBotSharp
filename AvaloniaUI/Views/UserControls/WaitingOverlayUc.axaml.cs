using MainCore.UI.ViewModels.UserControls;

using ReactiveUI;
using ReactiveUI.Avalonia;


namespace AvaloniaUI.Views.UserControls
{
    using ReactiveUI.Primitives;

    public class WaitingOverlayUcBase : ReactiveUserControl<WaitingOverlayViewModel>
    {
    }

    /// <summary>
    /// Code-behind for WaitingOverlayUc.axaml
    /// </summary>
    public partial class WaitingOverlayUc : WaitingOverlayUcBase
    {
        public WaitingOverlayUc()
        {
            InitializeComponent();

            this.WhenActivated(d =>
            {
                this.OneWayBind(ViewModel, vm => vm.Shown, v => v.Grid.IsVisible).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.Message, v => v.Message.Text).DisposeWith(d);
            });
        }
    }
}