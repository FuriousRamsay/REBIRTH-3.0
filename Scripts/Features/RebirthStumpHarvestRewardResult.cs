#nullable disable

public enum RebirthStumpHarvestRewardResultKind
{
    Unknown = 0,
    Disabled = 1,
    NoContext = 2,
    RejectedNotVehicleAttached = 3,
    NoPlayer = 4,
    InvalidReward = 5,
    InventoryFull = 6,
    PreviewEligible = 7,
    Granted = 8,
    IndeterminateAfterCommit = 9
}

public struct RebirthStumpHarvestRewardResult
{
    public RebirthStumpHarvestRewardResultKind Kind;
    public Vector3i BlockPos;
    public int PlayerEntityId;
    public string RewardItemName;
    public int RewardCount;
    public bool ContextConsumed;
    public bool VehicleAttached;
    public string Message;
}
