namespace MainCore.Features.Telegram;

/// <summary>A village as the sidebar showed it.</summary>
public sealed record ObservedVillage(int Id, string Name, int X, int Y, bool UnderAttack);

/// <summary>
/// Decides when an observation is worth a message, so the same attack or the same full warehouse is reported once,
/// not on every page refresh. State lives in memory: after a restart an ongoing attack is reported once more.
/// </summary>
public sealed class TelegramAlertRules
{
    /// <summary>Warn when a resource reaches this share of its storage.</summary>
    public const int StorageAlertPercent = 90;

    /// <summary>Warn again only after every resource fell to this share or lower.</summary>
    public const int StorageRearmPercent = 80;

    private readonly Lock _gate = new();
    private readonly Dictionary<(int Account, int Village), bool> _underAttack = [];
    private readonly HashSet<(int Account, int Village)> _storageAlerted = [];

    /// <summary>Villages that are under attack now but weren't at the last observation (or weren't seen yet).</summary>
    public IReadOnlyList<ObservedVillage> NewAttacks(AccountId accountId, IEnumerable<ObservedVillage> villages)
    {
        var attacked = new List<ObservedVillage>();
        lock (_gate)
        {
            foreach (var village in villages)
            {
                var key = (accountId.Value, village.Id);
                var wasUnderAttack = _underAttack.GetValueOrDefault(key);
                _underAttack[key] = village.UnderAttack;
                if (village.UnderAttack && !wasUnderAttack) attacked.Add(village);
            }
        }
        return attacked;
    }

    /// <summary>
    /// The resources at or above <see cref="StorageAlertPercent"/> the first time any of them gets there,
    /// or an empty list when there is nothing new to report.
    /// </summary>
    public IReadOnlyList<(string Resource, int Percent)> NearlyFull(AccountId accountId, VillageId villageId, StorageDto storage)
    {
        var percents = new (string Resource, int Percent)[]
        {
            ("wood", Percent(storage.Wood, storage.Warehouse)),
            ("clay", Percent(storage.Clay, storage.Warehouse)),
            ("iron", Percent(storage.Iron, storage.Warehouse)),
            ("crop", Percent(storage.Crop, storage.Granary)),
        };
        var key = (accountId.Value, villageId.Value);

        lock (_gate)
        {
            if (percents.All(x => x.Percent <= StorageRearmPercent))
            {
                _storageAlerted.Remove(key);
                return [];
            }

            var full = percents.Where(x => x.Percent >= StorageAlertPercent).ToList();
            if (full.Count == 0 || !_storageAlerted.Add(key)) return [];
            return full;
        }
    }

    private static int Percent(long amount, long capacity) =>
        capacity <= 0 ? 0 : (int)Math.Min(100, amount * 100 / capacity);
}
