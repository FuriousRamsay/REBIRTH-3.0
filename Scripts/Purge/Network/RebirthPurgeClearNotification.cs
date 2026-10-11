using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

// Subscribe to live transitions only. Loading/joining a saved ledger is silent.
internal static class RebirthPurgeClearNotification
{
    private static RebirthPoiWorldStore owner;
    internal static void Reset(){if(owner!=null)owner.PublicationChanged-=Changed;owner=null;}
    internal static void Pulse()
    {
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge){Reset();return;}
        RebirthPoiWorldStore current;
        if(!RebirthPoiWorldLifecycle.Instance.TryGetEvidenceStore(out current))return;
        if(ReferenceEquals(current,owner))return;
        Reset();owner=current;owner.PublicationChanged+=Changed;
    }
    private static void Changed(RebirthPoiWorldSnapshot before,RebirthPoiWorldSnapshot after,IReadOnlyList<RebirthPoiIdentity> changed)
    {
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||!after.Binding.IsCurrent)return;
        foreach(var identity in changed)
        {
            RebirthPoiClearanceRecord prior,next;
            if(!after.TryGet(identity,out next)||next.State!=RebirthPoiClearanceState.Cleared||next.Clear==null)continue;
            if(before.TryGet(identity,out prior)&&(prior.State==RebirthPoiClearanceState.Cleared||
                prior.State==RebirthPoiClearanceState.ResetPending&&prior.BeforeResetClear!=null))continue;
            Show(identity.Prefab,identity.Biome,next.Clear.SpawnedParticipants);
            var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(connection!=null&&connection.IsServer)
                connection.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPurgeClearNotice>().Setup(identity.Prefab,identity.Biome,next.Clear.SpawnedParticipants));
        }
    }
    internal static void Show(string prefab,string biome,int count)
    {
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge)return;
        var player=GameManager.Instance?.World?.GetPrimaryPlayer();if(player==null)return;
        GameManager.ShowTooltip(player,string.Format(Localization.Get("xuiRebirthPurgePoiCleared"),Localization.Get(prefab),count,Localization.Get("biome_"+biome)));
        Audio.Manager.PlayInsidePlayerHead("purge_discovered");
    }
}
[Preserve]
public sealed class NetPackageRebirthPurgeClearNotice:NetPackage
{
    private string prefab,biome;private int count;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthPurgeClearNotice Setup(string name,string region,int killed){prefab=name;biome=region;count=killed;return this;}
    public override void read(PooledBinaryReader reader){prefab=reader.ReadString();biome=reader.ReadString();count=reader.ReadInt32();}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write(prefab);writer.Write(biome);writer.Write(count);}
    public int GetLength()=>12+(prefab?.Length??0)*3+(biome?.Length??0)*3;
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null||!world.IsRemote()||!ReferenceEquals(GameManager.Instance.World,world)||
            prefab==null||prefab.Length>256||biome==null||biome.Length>64||count<1||count>1000000)return;
        RebirthPurgeClearNotification.Show(prefab,biome,count);
    }
}
