using Avalonia.Controls;

using MainCore.UI.ViewModels;
using MainCore.UI.ViewModels.UserControls;

using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace AvaloniaUI.Views
{
    public class MainWindowBase : ReactiveWindow<MainViewModel>
    {
    }

    public partial class MainWindow : MainWindowBase
    {
        private bool _canClose = false;
        private bool _isClosing = false;
        private bool _isLoaded = false;

        // Parameterless constructor for the XAML loader / previewer only.
        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(MainViewModel mainViewModel, WaitingOverlayViewModel waitingOverlayViewModel)
        {
            InitializeComponent();
            Opened += OnOpened;
            Closing += OnClosing;

            ViewModel = mainViewModel;
            WaitingOverlay.ViewModel = waitingOverlayViewModel;

            this.WhenActivated(d =>
            {
                this.OneWayBind(ViewModel, vm => vm.MainLayoutViewModel, v => v.MainLayout.ViewModel).DisposeWith(d);
            });
        }

        private async void OnOpened(object? sender, EventArgs e)
        {
            if (_isLoaded || ViewModel is null) return;
            try
            {
                await ViewModel.Load();
            }
            catch (Exception ex)
            {
                // e.g. Google Chrome missing; show why instead of leaving the overlay up forever.
                Serilog.Log.Error(ex, "Failed to load");
                await MessageDialog.Show(this, "Error", ex.Message, confirm: false);
            }
            finally
            {
                // Allow the window to close even when loading failed.
                _isLoaded = true;
            }
        }

        private async void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            if (!_isLoaded)
            {
                e.Cancel = true;
                return;
            }

            if (_canClose) return;
            e.Cancel = true;
            if (_isClosing || ViewModel is null) return;
            _isClosing = true;

            await ViewModel.Unload();

            _canClose = true;

            Signal.After(TimeSpan.FromMilliseconds(100), RxSchedulers.MainThreadScheduler).Subscribe(_ => Close());
        }
    }
}
