using MainCore.UI.ViewModels.Tabs;

using ReactiveUI;
using ReactiveUI.Avalonia;

namespace AvaloniaUI.Views.Tabs
{
    using ReactiveUI.Primitives;

    public class AccountSettingTabBase : ReactiveUserControl<AccountSettingViewModel>
    {
    }

    /// <summary>
    /// Code-behind for AccountSettingTab.axaml
    /// </summary>
    public partial class AccountSettingTab : AccountSettingTabBase
    {
        public AccountSettingTab()
        {
            InitializeComponent();
            this.WhenActivated(d =>
            {
                this.BindCommand(ViewModel, vm => vm.ExportCommand, v => v.ExportButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.ImportCommand, v => v.ImportButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.SaveCommand, v => v.SaveButton).DisposeWith(d);

                this.OneWayBind(ViewModel, vm => vm.AccountSettingInput.ClickDelay, v => v.ClickDelay.ViewModel).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.AccountSettingInput.TaskDelay, v => v.TaskDelay.ViewModel).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.AccountSettingInput.WorkTime, v => v.WorkTime.ViewModel).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.AccountSettingInput.SleepTime, v => v.SleepTime.ViewModel).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.EnableAutoLoadVillage, v => v.EnableAutoLoadVillage.IsChecked).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.HeadlessChrome, v => v.HeadlessChrome.IsChecked).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.EnableAutoStartAdventure, v => v.EnableAutoStartAdventure.IsChecked).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.EnableAutoClaimDailyQuest, v => v.EnableAutoClaimDailyQuest.IsChecked).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.EnableAutoSetHeroPoint, v => v.EnableAutoSetHeroPoint.IsChecked).DisposeWith(d);

                this.Bind(ViewModel, vm => vm.AccountSettingInput.LogoutWhenWorkEnds, v => v.LogoutWhenWorkEnds.IsChecked).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.EnableNightSleep, v => v.EnableNightSleep.IsChecked).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.NightSleepStart, v => v.NightSleepStart.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.NightSleepEnd, v => v.NightSleepEnd.Text).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.AccountSettingInput.NightSleepJitter, v => v.NightSleepJitter.ViewModel).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.TimeZone, v => v.TimeZoneTextBox.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.Language, v => v.LanguageTextBox.Text).DisposeWith(d);

                this.Bind(ViewModel, vm => vm.AccountSettingInput.EnableTelegram, v => v.EnableTelegram.IsChecked).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.TelegramBotToken, v => v.TelegramBotToken.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.TelegramChatId, v => v.TelegramChatId.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.TelegramNotifyAttack, v => v.TelegramNotifyAttack.IsChecked).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.TelegramNotifyPaused, v => v.TelegramNotifyPaused.IsChecked).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.TelegramNotifyStorage, v => v.TelegramNotifyStorage.IsChecked).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.FindTelegramChatCommand, v => v.FindTelegramChatButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.SendTelegramTestCommand, v => v.SendTelegramTestButton).DisposeWith(d);
            });
        }
    }
}