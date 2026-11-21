using FluentValidation;

using MainCore.Features.Telegram;
using MainCore.UI.Models.Input;

namespace MainCore.UI.Models.Validators
{
    public class AccountSettingInputValidator : AbstractValidator<AccountSettingInput>
    {
        public AccountSettingInputValidator()
        {
            RuleFor(x => x.ClickDelay.Min)
                .LessThanOrEqualTo(x => x.ClickDelay.Max)
                .WithMessage("Minimum click delay ({PropertyValue}) should be less than maximum click delay ({ComparisonValue})");
            RuleFor(x => x.ClickDelay.Min)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Minimum click delay ({PropertyValue}) should be positive number");
            RuleFor(x => x.TaskDelay.Min)
                .LessThanOrEqualTo(x => x.TaskDelay.Max)
                .WithMessage("Minimum task delay ({PropertyValue}) should be less than maximum task delay ({ComparisonValue})");
            RuleFor(x => x.TaskDelay.Min)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Minimum task delay ({PropertyValue}) should be positive number");

            RuleFor(x => x.FarmInterval.Min)
                .LessThanOrEqualTo(x => x.FarmInterval.Max)
                .WithMessage("Minimum farm interval ({PropertyValue}) should be less than maximum farm interval ({ComparisonValue})");
            RuleFor(x => x.FarmInterval.Min)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Minimum farm interval ({PropertyValue}) should be positive number");

            RuleFor(x => x.WorkTime.Min)
                .GreaterThan(0)
                .WithMessage("Minimum work time ({PropertyValue}) should be at least 1 hour");
            RuleFor(x => x.WorkTime.Min)
                .LessThanOrEqualTo(x => x.WorkTime.Max)
                .WithMessage("Minimum work time ({PropertyValue}) should be less than maximum work time ({ComparisonValue})");
            RuleFor(x => x.SleepTime.Min)
                .LessThanOrEqualTo(x => x.SleepTime.Max)
                .WithMessage("Minimum sleep time ({PropertyValue}) should be less than maximum sleep time ({ComparisonValue})");

            RuleFor(x => x.FarmIdle.Min)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Minimum farm idle gap ({PropertyValue}) should be positive number");
            RuleFor(x => x.FarmIdle.Min)
                .LessThanOrEqualTo(x => x.FarmIdle.Max)
                .WithMessage("Minimum farm idle gap ({PropertyValue}) should be less than maximum farm idle gap ({ComparisonValue})");

            RuleFor(x => x.NightSleepStart)
                .Must(x => AccountSettingInput.TryParseTime(x, out _))
                .WithMessage("Night sleep start ({PropertyValue}) should be a time like 23:30");
            RuleFor(x => x.NightSleepEnd)
                .Must(x => AccountSettingInput.TryParseTime(x, out _))
                .WithMessage("Night sleep end ({PropertyValue}) should be a time like 07:00");
            RuleFor(x => x.NightSleepJitter.Value)
                .InclusiveBetween(0, 180)
                .WithMessage("Night sleep shift ({PropertyValue}) should be between 0 and 180 minutes");

            RuleFor(x => x.TimeZone)
                .Must(x => string.IsNullOrWhiteSpace(x) || AccountLocale.FindZone(x) is not null)
                .WithMessage("Unknown time zone ({PropertyValue}). Use a name like Asia/Bangkok or Europe/Prague, or leave it empty for auto");
            RuleFor(x => x.Language)
                .Must(x => string.IsNullOrWhiteSpace(x) || AccountLocale.IsValidLanguage(x))
                .WithMessage("Unknown language ({PropertyValue}). Use a code like en-US or th-TH, or leave it empty for auto");

            RuleFor(x => x.TelegramBotToken)
                .Must(TelegramClient.IsValidToken)
                .When(x => x.EnableTelegram)
                .WithMessage("Telegram is on, but the bot token doesn't look right. It's the text @BotFather gave you, like 123456789:ABC...");
            RuleFor(x => x.TelegramChatId)
                .Must(TelegramClient.IsValidChatId)
                .When(x => x.EnableTelegram)
                .WithMessage("Telegram is on, but the chat ID is missing or wrong. Use Find chat, or enter a number like 123456789");
        }
    }
}