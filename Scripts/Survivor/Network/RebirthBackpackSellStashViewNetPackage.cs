using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthBackpackSellStashViewRequest : NetPackage
{
    private int playerId;
    private string creation;
    private Guid request;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthBackpackSellStashViewRequest Setup(int id,Guid character,Guid token) =>Setup(id,character.ToString("N"),token);
    public NetPackageRebirthBackpackSellStashViewRequest Setup(int id,string character,Guid token)
    {playerId=id;RebirthSurvivorRequestScope.TryNormalize(character,out creation);request=token;return this;}
    public override void read(PooledBinaryReader reader)
    {
        creation=null;request=Guid.Empty;playerId=reader.ReadInt32();
        string current=RebirthBackpackLibraryCreationWire.Read(reader);var r=reader.ReadBytes(16);
        if(r.Length!=16)throw new InvalidDataException("Truncated library view request.");
        creation=current;request=new Guid(r);
    }
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);((BinaryWriter)writer).Write(playerId);RebirthBackpackLibraryCreationWire.Write((BinaryWriter)writer,creation);((BinaryWriter)writer).Write(request.ToByteArray());}
    public int GetLength()=>22+RebirthBackpackLibraryCreationWire.Length(creation);
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(world==null||world.IsRemote()||Sender==null||creation==null||request==Guid.Empty||
            !ValidEntityIdForSender(playerId)||manager==null||!manager.IsServer)return;
        try
        {
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackSellStashView));
        var player=world.GetEntity(playerId) as EntityPlayer;
        var status=RebirthBackpackLibraryServer.GetSellStashViewStatus(player,Sender,creation,out var view,out long revision);
        // Authentication failure is indistinguishable from unavailable state; it cannot expose contents.
        if(!RebirthBackpackSellStashViewResponse.TryCreate(creation,request,revision,status,view,out var response))return;
        manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthBackpackSellStashView>().Setup(playerId,response),_attachedToEntityId:playerId);
        }
        catch(Exception error)
        {Log.Warning("[REBIRTH Sell Stash] Owner view reply deferred: "+error.GetType().Name);}
    }
}

[Preserve]
public sealed class NetPackageRebirthBackpackSellStashView : NetPackage
{
    private int playerId;
    private RebirthBackpackSellStashViewResponse response;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthBackpackSellStashView Setup(int id,RebirthBackpackSellStashViewResponse value)
    {playerId=id;response=value??throw new ArgumentNullException(nameof(value));return this;}
    public override void read(PooledBinaryReader reader)
    {response=null;playerId=reader.ReadInt32();if(!RebirthBackpackSellStashViewResponse.TryRead(reader,out response))throw new InvalidDataException("Invalid library owner view.");}
    public override void write(PooledBinaryWriter writer)
    {if(response==null)throw new InvalidOperationException("Missing library owner view.");base.write(writer);((BinaryWriter)writer).Write(playerId);response.Write((BinaryWriter)writer);}
    public int GetLength()=>6+(response?.BodyLength??0);
    public override void ProcessPackage(World world,GameManager callbacks)
    {RebirthBackpackSellStashClientViews.Receive(world,playerId,response);}
}