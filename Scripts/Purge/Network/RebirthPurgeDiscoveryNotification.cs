using System;
using UnityEngine.Scripting;

// Called only after a live player-driven discovery is published. Census/bulk
// discovery and replay never call this path and remain silent.
internal static class RebirthPurgeDiscoveryNotification
{
    internal static string DisplayName(EntityPlayer player)
    {
        return CleanDisplayName(player?.EntityName);
    }
    internal static string CleanDisplayName(string value)
    {
        string name = value ?? "";
        name = name.Replace("[", "").Replace("]", "").Replace("\r", " ").Replace("\n", " ").Trim();
        return name.Length > 80 ? name.Substring(0, 80) : name;
    }

    internal static void Send(RebirthPoiIdentity identity, EntityPlayer discoverer)
    {
        var world = GameManager.Instance?.World;
        if (!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.IsPurge ||
            identity == null || world == null || world.IsRemote() || discoverer == null ||
            !discoverer.IsSpawned() || discoverer.IsDead() || discoverer.IsSpectator || discoverer.AttachedToEntity != null ||
            discoverer.IsFlyMode.Value || RebirthPurgeKillContributor.Identify(discoverer, world) == null) return;
        string name = DisplayName(discoverer);
        RebirthPurgePoiHistory.Discover(identity,name);
        Show(identity.Prefab, identity.Biome, name);
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection != null && connection.IsServer)
            connection.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPurgeDiscoveryNotice>()
                .Setup(identity.Prefab, identity.Biome, name));
    }

    internal static void Show(string prefab, string biome, string discoverer)
    {
        if (!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.IsPurge) return;
        var player = GameManager.Instance?.World?.GetPrimaryPlayer();
        if (player == null) return;
        GameManager.ShowTooltip(player, string.Format(Localization.Get("xuiRebirthPurgePoiDiscoveredNotice"),
            discoverer, Localization.Get(prefab), Localization.Get("biome_" + biome)));
        Audio.Manager.PlayInsidePlayerHead("purge_cleared");
    }
}

[Preserve]
public sealed class NetPackageRebirthPurgeDiscoveryNotice : NetPackage
{
    private Guid noticeWorld, noticeSession;
    private string prefab, biome, discoverer;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    internal NetPackageRebirthPurgeDiscoveryNotice Setup(string name, string region, string player)
    { noticeWorld=RebirthPoiMapSync.LocalWorld; noticeSession=RebirthPoiMapSync.LocalSession; prefab = name; biome = region; discoverer = player; return this; }
    public override void read(PooledBinaryReader reader)
    { noticeWorld=new Guid(reader.ReadBytes(16)); noticeSession=new Guid(reader.ReadBytes(16)); prefab = RebirthSurvivorNetworkCodec.ReadBoundedString(reader,256); biome = RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64); discoverer = RebirthSurvivorNetworkCodec.ReadBoundedString(reader,80); }
    public override void write(PooledBinaryWriter writer)
    { base.write(writer); writer.Write(noticeWorld.ToByteArray()); writer.Write(noticeSession.ToByteArray()); RebirthSurvivorNetworkCodec.WriteString(writer,prefab,256); RebirthSurvivorNetworkCodec.WriteString(writer,biome,64); RebirthSurvivorNetworkCodec.WriteString(writer,discoverer,80); }
    public int GetLength() => 48 + ((prefab?.Length ?? 0) + (biome?.Length ?? 0) + (discoverer?.Length ?? 0)) * 3;
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || !world.IsRemote() || !ReferenceEquals(GameManager.Instance?.World, world) ||
            string.IsNullOrEmpty(prefab) || prefab.Length > 256 || string.IsNullOrEmpty(biome) || biome.Length > 64 ||
            discoverer == null || discoverer.Length > 80) return;
        string name=prefab,region=biome,player=discoverer;
        RebirthPurgeNoticeDelivery.Receive(world,noticeWorld,noticeSession,()=>RebirthPurgeDiscoveryNotification.Show(name,region,player));
    }
}
