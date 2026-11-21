using Avalonia;

using MainCore.UI.ViewModels.UserControls;

using ReactiveUI;
using ReactiveUI.Avalonia;

namespace AvaloniaUI.Views.UserControls
{
    using ReactiveUI.Primitives;

    public class TroopSelectorUcBase : ReactiveUserControl<TroopSelectorViewModel>
    {
    }

    /// <summary>
    /// Code-behind for TroopSelectorUc.axaml
    /// </summary>
    public partial class TroopSelectorUc : TroopSelectorUcBase
    {
        public TroopSelectorUc()
        {
            InitializeComponent();
            this.WhenActivated(d =>
            {
                this.OneWayBind(ViewModel, vm => vm.Items, v => v.TroopComboBox.ItemsSource).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.SelectedItem, v => v.TroopComboBox.SelectedItem).DisposeWith(d);
            });
        }

        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<TroopSelectorUc, string?>(nameof(Text));

        public string? Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
    }
}