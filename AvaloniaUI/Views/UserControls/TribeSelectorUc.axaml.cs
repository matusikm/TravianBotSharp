using Avalonia;

using MainCore.UI.ViewModels.UserControls;

using ReactiveUI;
using ReactiveUI.Avalonia;

namespace AvaloniaUI.Views.UserControls
{
    using ReactiveUI.Primitives;

    public class TribeSelectorUcBase : ReactiveUserControl<TribeSelectorViewModel>
    {
    }

    /// <summary>
    /// Code-behind for TribeSelectorUc.axaml
    /// </summary>
    public partial class TribeSelectorUc : TribeSelectorUcBase
    {
        public TribeSelectorUc()
        {
            InitializeComponent();
            this.WhenActivated(d =>
            {
                this.OneWayBind(ViewModel, vm => vm.Items, v => v.TribeComboBox.ItemsSource).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.SelectedItem, v => v.TribeComboBox.SelectedItem).DisposeWith(d);
            });
        }

        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<TribeSelectorUc, string?>(nameof(Text));

        public string? Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
    }
}