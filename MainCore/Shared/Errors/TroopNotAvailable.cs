namespace MainCore.Shared.Errors;

/// <summary>The building has no training row for the troop, usually because it isn't researched yet.</summary>
public sealed class TroopNotAvailable : Error
{
    private TroopNotAvailable(TroopEnums troop) : base($"{troop} can't be trained in this building yet")
    {
        Troop = troop;
    }

    public TroopEnums Troop { get; }

    public static Result Error(TroopEnums troop) => new TroopNotAvailable(troop);
}
