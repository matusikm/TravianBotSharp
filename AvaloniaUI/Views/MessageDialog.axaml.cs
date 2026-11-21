using Avalonia.Controls;

namespace AvaloniaUI.Views
{
    /// <summary>Replacement for the WPF MessageBox (OK, or Yes/No when confirming).</summary>
    public partial class MessageDialog : Window
    {
        public MessageDialog()
        {
            InitializeComponent();
        }

        public static async Task<bool> Show(Window? owner, string title, string message, bool confirm)
        {
            var dialog = new MessageDialog { Title = title };
            dialog.MessageText.Text = message;
            dialog.YesButton.IsVisible = confirm;
            dialog.NoButton.IsVisible = confirm;
            dialog.OkButton.IsVisible = !confirm;
            dialog.YesButton.Click += (_, _) => dialog.Close(true);
            dialog.NoButton.Click += (_, _) => dialog.Close(false);
            dialog.OkButton.Click += (_, _) => dialog.Close(true);

            if (owner is { IsVisible: true })
            {
                return await dialog.ShowDialog<bool>(owner);
            }

            var closed = new TaskCompletionSource<bool>();
            dialog.Closed += (_, _) => closed.TrySetResult(false);
            dialog.Show();
            await closed.Task;
            return false;
        }
    }
}
