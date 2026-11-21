namespace MainCore.Shared.Notifications
{
    /// <summary>The bot paused the account on its own (an error, or no internet for too long), not the user.</summary>
    public record AccountNeedsAttention(AccountId AccountId, string Reason) : IAccountNotification;
}
