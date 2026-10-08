using System;
using System.Linq;
using System.IO;
using UnityEngine.Scripting;

// Small read-only public profile projection. Never send the private owner snapshot.
public sealed class RebirthPublicPlayerProfile
{
    public int EntityId;
    public string Background="", Identity="", BackgroundId="";
    public string[] Names=new string[4], Icons=new string[4];
    public float[] Values=new float[4];
    public static RebirthPublicPlayerProfile Build(EntityPlayer player,RebirthSurvivorOwnerStateSnapshot owner)
    {
        var result=new RebirthPublicPlayerProfile{EntityId=player.entityId};
        var snapshot=RebirthCharacterUiSnapshotBuilder.Build(player,owner,false,default(RebirthMetabolismSnapshot),true);
        if(snapshot==null){result.Background="No background available";return result;}
        result.BackgroundId=snapshot.BackgroundId;result.Background=snapshot.BackgroundName;result.Identity=snapshot.BackgroundIdentity;
        var skills=snapshot.Skills.OrderByDescending(s=>s.Practical).ThenBy(s=>s.Name).Take(4).ToArray();
        for(int i=0;i<skills.Length;i++){result.Names[i]=skills[i].Name;result.Icons[i]=skills[i].Icon;result.Values[i]=skills[i].Practical;}
        return result;
    }
    public static RebirthPublicPlayerProfile FromServer(EntityPlayer player)
    {
        RebirthStablePlayerIdentity identity;
        return player!=null && RebirthStablePlayerIdentity.TryResolveServerEntity(player,out identity)?Build(player,RebirthSurvivorNetworkService.CaptureOwnerState(identity)):null;
    }
    public static event Action<RebirthPublicPlayerProfile> Received;
    public static void Receive(RebirthPublicPlayerProfile value){Received?.Invoke(value);}
}
[Preserve]
public sealed class NetPackageRebirthPublicProfileRequest : NetPackage
{
    private int requester,target;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthPublicProfileRequest Setup(int from,int to){requester=from;target=to;return this;}
    public override void read(PooledBinaryReader reader){requester=reader.ReadInt32();target=reader.ReadInt32();}
    public override void write(PooledBinaryWriter writer){base.write(writer);((BinaryWriter)writer).Write(requester);((BinaryWriter)writer).Write(target);}
    public int GetLength()=>12;
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null||world.IsRemote()||!ValidEntityIdForSender(requester)||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        var player=world.GetEntity(target) as EntityPlayer;
        var profile=RebirthPublicPlayerProfile.FromServer(player);
        if(profile!=null)SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPublicProfileResponse>().Setup(profile),_attachedToEntityId:requester);
    }
}
[Preserve]
public sealed class NetPackageRebirthPublicProfileResponse : NetPackage
{
    private RebirthPublicPlayerProfile value;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthPublicProfileResponse Setup(RebirthPublicPlayerProfile profile){value=profile;return this;}
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);((BinaryWriter)writer).Write(value.EntityId);((BinaryWriter)writer).Write(value.Background??"");((BinaryWriter)writer).Write(value.Identity??"");((BinaryWriter)writer).Write(value.BackgroundId??"");
        for(int i=0;i<4;i++){((BinaryWriter)writer).Write(value.Names[i]??"");((BinaryWriter)writer).Write(value.Icons[i]??"");((BinaryWriter)writer).Write(value.Values[i]);}
    }
    public override void read(PooledBinaryReader reader)
    {
        value=new RebirthPublicPlayerProfile{EntityId=reader.ReadInt32(),Background=reader.ReadString(),Identity=reader.ReadString(),BackgroundId=reader.ReadString()};
        for(int i=0;i<4;i++){value.Names[i]=reader.ReadString();value.Icons[i]=reader.ReadString();value.Values[i]=reader.ReadSingle();}
    }
    public int GetLength()=>32+(value==null?0:(value.Background.Length+value.Identity.Length+value.Names.Sum(s=>s?.Length??0)+value.Icons.Sum(s=>s?.Length??0))*3);
    public override void ProcessPackage(World world,GameManager callbacks){if(world!=null&&world.IsRemote())RebirthPublicPlayerProfile.Receive(value);}
}
