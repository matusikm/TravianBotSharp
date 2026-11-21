namespace MainCore.Shared.Notifications
{
    /// <summary>A village's resources and capacities were just read from the page.</summary>
    public record StorageModified(AccountId AccountId, VillageId VillageId, StorageDto Storage) : IAccountVillageNotification;
}
