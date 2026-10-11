using System;

#nullable disable

public enum RebirthPoiRiskMode
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3
}


public struct RebirthPoiRiskDisplaySnapshot
{
    public bool InsidePoi;
    public bool Applied;
    public string PrefabName;
    public int PoiTier;
    public int PersonalGameStage;
    public int NativeSleeperGameStage;
    public int Bonus;
    public int EffectiveSleeperGameStage;
    public RebirthPoiRiskMode Mode;
    public RebirthSpawnProgressionMode ProgressionMode;
}

/// <summary>
/// Server-authoritative, POI-local sleeper gamestage policy. It never mutates a
/// player's persistent/global gamestage. The native SleeperVolume gamestage is
/// augmented only while that volume resolves its sleeper groups.
/// </summary>
public static class RebirthPoiRiskRuntimePolicy
{
    private static RebirthPoiRiskMode s_mode = RebirthPoiRiskMode.Medium;
    public const int MaximumGameStage = 600;
    private static readonly int[] TierBonus = { 0, 1, 30, 60, 90, 120, 150, 180 };

    public static RebirthPoiRiskMode Mode { get { return s_mode; } }
    public static void SetMode(RebirthPoiRiskMode mode)
    {
        s_mode = Enum.IsDefined(typeof(RebirthPoiRiskMode), mode) ? mode : RebirthPoiRiskMode.Medium;
    }

    public static int CalculateEffectiveSleeperGameStage(int nativeGameStage, int poiTier)
    {
        int baseGs = Math.Max(0, nativeGameStage);
        int bonus = CalculateBonus(baseGs, poiTier, s_mode);
        long result = (long)baseGs + bonus;
        return (int)Math.Min(int.MaxValue, result);
    }

    public static int CalculateBonus(int nativeGameStage, int poiTier, RebirthPoiRiskMode mode)
    {
        if (mode == RebirthPoiRiskMode.None || poiTier <= 0) return 0;

        // SleeperVolume.gameStage comes from nearby EntityPlayer.gameStage values. In 3.1,
        // EntityPlayer.gameStage already applies the current biome's gamestage modifier and
        // flat bonus. Adding the old 2.6 Rebirth biome table here would count biome danger
        // twice, so POI Risk now contributes only the POI-tier component.
        int tier = Math.Max(1, Math.Min(7, poiTier));
        int raw = TierBonus[tier];
        float scale = mode == RebirthPoiRiskMode.Low ? 0.5f : mode == RebirthPoiRiskMode.High ? 1.5f : 1f;
        int cappedGs = Math.Min(Math.Max(0, nativeGameStage), MaximumGameStage);
        double remainingFactor = (double)(MaximumGameStage - cappedGs) / MaximumGameStage;
        return Math.Max(0, (int)(raw * scale * remainingFactor));
    }

    public static int GetPoiTier(SleeperVolume volume)
    {
        PrefabInstance prefabInstance = volume != null ? volume.prefabInstance : null;
        Prefab prefab = prefabInstance != null ? prefabInstance.prefab : null;
        return prefab != null ? prefab.DifficultyTier : 0;
    }

    /// <summary>
    /// Produces the client-visible POI gamestage from the same tier policy used by
    /// the authoritative sleeper patch. This never writes EntityPlayer.gameStage.
    /// </summary>
    public static bool TryGetDisplaySnapshot(EntityPlayer player, out RebirthPoiRiskDisplaySnapshot snapshot)
    {
        snapshot = default(RebirthPoiRiskDisplaySnapshot);
        if (player == null || player.world == null) return false;

        snapshot.PersonalGameStage = Math.Max(1, player.gameStage);
        snapshot.NativeSleeperGameStage = snapshot.PersonalGameStage;
        snapshot.EffectiveSleeperGameStage = snapshot.PersonalGameStage;
        snapshot.Mode = s_mode;
        snapshot.ProgressionMode = RebirthSandboxOptionManager.Current.EffectiveSpawnProgression;

        PrefabInstance prefabInstance = null;
        try
        {
            prefabInstance = player.world.GetPOIAtPosition(player.position);
        }
        catch
        {
            return false;
        }

        Prefab prefab = prefabInstance != null ? prefabInstance.prefab : null;
        int tier = prefab != null ? prefab.DifficultyTier : 0;
        if (tier <= 0) return false;

        snapshot.InsidePoi = true;
        snapshot.PoiTier = tier;
        snapshot.PrefabName = prefabInstance != null ? prefabInstance.name : string.Empty;

        int nativeGameStage = snapshot.PersonalGameStage;
        try
        {
            int around = GameStageDefinition.CalcGameStageAround(player);
            if (around > 0) nativeGameStage = around;
        }
        catch
        {
            nativeGameStage = snapshot.PersonalGameStage;
        }

        snapshot.NativeSleeperGameStage = nativeGameStage;
        snapshot.Applied = snapshot.ProgressionMode == RebirthSpawnProgressionMode.Gamestage
            && snapshot.Mode != RebirthPoiRiskMode.None;
        snapshot.Bonus = snapshot.Applied ? CalculateBonus(nativeGameStage, tier, snapshot.Mode) : 0;
        snapshot.EffectiveSleeperGameStage = nativeGameStage + snapshot.Bonus;
        return true;
    }

    public static int GetDisplayedGameStage(EntityPlayer player)
    {
        RebirthPoiRiskDisplaySnapshot snapshot;
        return TryGetDisplaySnapshot(player, out snapshot) && snapshot.Applied
            ? snapshot.EffectiveSleeperGameStage
            : (player != null ? Math.Max(1, player.gameStage) : 1);
    }

}
