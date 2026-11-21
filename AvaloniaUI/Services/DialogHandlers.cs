using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using AvaloniaUI.Views;

using MainCore.Services;

using ReactiveUI.Primitives;

namespace AvaloniaUI.Services
{
    /// <summary>Avalonia implementations of the dialogs MainCore asks for through <see cref="DialogService"/>.</summary>
    internal static class DialogHandlers
    {
        private static Func<Window?> _owner = () => null;

        private static readonly IReadOnlyList<FilePickerFileType> TbsFileTypes =
        [
            new("TBS files") { Patterns = ["*.tbs"] },
            FilePickerFileTypes.All,
        ];

        public static void Register(DialogService dialogService, Func<Window?> owner)
        {
            _owner = owner;

            dialogService.MessageBox.RegisterHandler(async context =>
            {
                await OnUiThread(() => MessageDialog.Show(_owner(), context.Input.Title, context.Input.Message, confirm: false));
                context.SetOutput(RxVoid.Default);
            });

            dialogService.ConfirmBox.RegisterHandler(async context =>
            {
                var result = await OnUiThread(() => MessageDialog.Show(_owner(), context.Input.Title, context.Input.Message, confirm: true));
                context.SetOutput(result);
            });

            dialogService.FileOpenDialog.RegisterHandler(async context =>
            {
                var result = await OnUiThread(OpenFile);
                context.SetOutput(result);
            });

            dialogService.FileSaveDialog.RegisterHandler(async context =>
            {
                var result = await OnUiThread(SaveFile);
                context.SetOutput(result);
            });
        }

        public static void ShowError(string title, string message)
        {
            Dispatcher.UIThread.Post(() => _ = MessageDialog.Show(_owner(), title, message, confirm: false));
        }

        private static Task<T> OnUiThread<T>(Func<Task<T>> action)
        {
            if (Dispatcher.UIThread.CheckAccess()) return action();
            return Dispatcher.UIThread.InvokeAsync(action);
        }

        private static async Task<string> OpenFile()
        {
            var storage = _owner()?.StorageProvider;
            if (storage is null) return "";
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = TbsFileTypes,
                SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(AppContext.BaseDirectory),
            });
            return files.Count == 0 ? "" : files[0].TryGetLocalPath() ?? "";
        }

        private static async Task<string> SaveFile()
        {
            var storage = _owner()?.StorageProvider;
            if (storage is null) return "";
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = TbsFileTypes,
                DefaultExtension = "tbs",
                SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(AppContext.BaseDirectory),
            });
            return file?.TryGetLocalPath() ?? "";
        }
    }
}
