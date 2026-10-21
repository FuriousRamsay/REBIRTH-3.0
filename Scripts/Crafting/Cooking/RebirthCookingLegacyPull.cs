using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public static partial class RebirthCookingLegacyPull
{
    internal const int MaximumIngredientRequests = 12;

    internal static List<ItemStack> ReadIngredientRequests(PooledBinaryReader reader)
    {
        int count = reader.ReadUInt16();
        if (count > MaximumIngredientRequests)
            throw new InvalidDataException("Cooking ingredient request exceeds its slot limit.");
        var result = new List<ItemStack>(count);
        for (int index = 0; index < count; index++)
        {
            var stack = new ItemStack();
            stack.Read(reader);
            result.Add(ItemClass.GetForId(stack.itemValue.type) == null ? ItemStack.Empty : stack);
        }
        return result;
    }

    private sealed class Original
    {internal EntityPlayerLocal Player;internal World World;internal string Owner,Creation;internal int Deaths=-1;internal bool MetadataOnly;internal long Request;internal List<ItemStack> Needs;internal float Next;}
    private static readonly Dictionary<long,Original> waiting=new Dictionary<long,Original>();
    private static bool installed;
    private static float bootstrapAt;private static World bootstrapWorld;private static string bootstrapDirectory;
    private static EntityPlayerLocal bootstrapPlayer;private static string bootstrapCreation;private static int bootstrapDeaths=-1;
    private static void Install(){if(installed)return;RebirthProductiveEarnedUiDeferredTransport.InstallSelectedSendObserver();ModEvents.GameUpdate.RegisterHandler(Tick);installed=true;}
    internal static void InstallRecovery()=>Install();
    private static bool CurrentPullSession(Original original)
    {
        return ThreadManager.IsMainThread()&&original?.Player!=null&&original.World!=null&&
            ReferenceEquals(GameManager.Instance?.World,original.World)&&ReferenceEquals(original.Player.world,original.World)&&
            ReferenceEquals(original.World.GetPrimaryPlayer(),original.Player)&&ReferenceEquals(original.World.GetEntity(original.Player.entityId),original.Player)&&
            !original.Player.IsDead()&&original.Creation==RebirthCamperCharacterAccess.CurrentCreation(original.Player)&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(original.Player,out var owner)&&owner.CanonicalId==original.Owner;
    }
    // Metadata queries can reconcile an ended-life original. Its old UI
    // callback cannot grant those items to a later life of the same body.
    private static bool CurrentDeliverySession(Original original)
        =>CurrentPullSession(original)&&!original.MetadataOnly&&original.Deaths>=0&&original.Deaths==original.Player.Died;
    private static void Send(Original original)
    {
        original.Next=Time.realtimeSinceStartup+3f;
        if(!CurrentPullSession(original))return;
        if(!original.World.IsRemote())
        {
            if(PrepareOriginalSend(original))SendLocalOriginal(original);
            return;
        }
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;var persistent=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(original.Player.entityId);
        if(connection?.IsServer!=false||!connection.IsConnected||persistent?.PrimaryId==null)return;
        if(!PrepareOriginalSend(original))return;
        NetPackageRebirthCookingLegacyPullOriginal.Send(original.Player.entityId,persistent.PrimaryId,original.Request,original.Needs,original.World.worldState.Guid,original.Creation,original.Deaths,!CurrentDeliverySession(original));
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        var current=GameManager.Instance?.World;string directory=GameIO.GetSaveGameDir();
        if(!ReferenceEquals(bootstrapWorld,current)||bootstrapDirectory!=directory)
        {waiting.Clear();bootstrapWorld=current;bootstrapDirectory=directory;bootstrapPlayer=null;bootstrapCreation=null;bootstrapDeaths=-1;bootstrapAt=0;}
        var player=current?.GetPrimaryPlayer();
        string creation=player==null?null:RebirthCamperCharacterAccess.CurrentCreation(player);
        if(!ReferenceEquals(bootstrapPlayer,player)||bootstrapCreation!=creation||bootstrapDeaths!=(player?.Died??-1))
        {
            if(!ReferenceEquals(bootstrapPlayer,player))waiting.Clear();
            bootstrapPlayer=player;bootstrapCreation=creation;bootstrapDeaths=player?.Died??-1;bootstrapAt=0;
        }
        if(current!=null&&player!=null&&player.IsSpawned()&&!player.IsDead()&&Time.realtimeSinceStartup>=bootstrapAt)
        {
            bootstrapAt=Time.realtimeSinceStartup+5f;
            try
            {
                if(RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))
                {
                    foreach(var record in ReadRequests())
                    {
                        if(!CurrentOriginal(player,identity,record))continue;
                        if(!record.Before.SequenceEqual(record.After)||record.Before.Length>4*1024*1024)throw new InvalidDataException();
                        var root=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),4*1024*1024);
                        int deaths=ReadRequestDeaths(root);
                        long request=(long)root.Attribute("request");if(request<=0||root.Name!="cookingPullOriginal"||root.Attributes().Count()!=(deaths<0?5:6)||root.Elements().Count()!=1||record.Operation!=Operation(player,identity.CanonicalId,request))throw new InvalidDataException();
                        if(waiting.ContainsKey(request))continue;
                        var needs=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("needs").Value));
                        if(needs.Length<1||needs.Length>12||needs.Any(item=>item==null||item.IsEmpty()||item.count<1))throw new InvalidDataException();
                        if(RebirthCookingGridCustody.HasFetchClaim(player,request,out bool complete)&&complete){CompleteGridOriginal(player,request);continue;}
                        waiting.Add(request,new Original{Player=player,World=current,Owner=identity.CanonicalId,Creation=record.OwnerCreation,Deaths=deaths,Request=request,Needs=needs.ToList()});
                    }
                    // New pulls register in waiting when written; cold originals need one completed scan.
                    bootstrapAt=float.PositiveInfinity;
                }
            }
            catch{/* Unknown retained claim is not a new withdrawal or grid grant. */}
        }
        if(waiting.Count==0)return;
        foreach(var original in waiting.Values.ToArray())
        {
            if(!ReferenceEquals(GameManager.Instance?.World,original.World)||!ReferenceEquals(original.World.GetPrimaryPlayer(),original.Player))
            {waiting.Remove(original.Request);continue;}
            if(Time.realtimeSinceStartup>=original.Next)try{Send(original);}catch{/* Same durable original remains pending. */}
        }
    }
    private static RebirthRemoteResourceClientOriginalJournal Requests()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingPullRequests"));
    private static RebirthRemoteResourceClientOriginalJournal Results()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingPullResults"));
    private static Guid Operation(EntityPlayerLocal player,string owner,long id)=>RebirthCookingRemoteAdmissionBackend.StationKey(player.world.worldState.Guid,owner+"|cookingPull|"+id);
    private static IEnumerable<RebirthCamperTransferJournal.Record> ReadRequests()
    {
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingPullRequests");if(!Directory.Exists(directory))yield break;int count=0;
        foreach(string file in Directory.EnumerateFiles(directory,"*.0.rcj"))
        {
            if(!Guid.TryParseExact(Path.GetFileName(file).Substring(0,32),"N",out var operation))throw new InvalidDataException();var record=Requests().Read(operation);if(record==null)throw new InvalidDataException();
            if(record.State==RebirthCamperTransferJournal.Phase.Applied||record.State==RebirthCamperTransferJournal.Phase.Rejected){Requests().Archive(operation);continue;}
            if(++count>16384)throw new InvalidDataException();yield return record;
        }
    }
    private static bool CurrentOriginal(EntityPlayerLocal player,RebirthStablePlayerIdentity identity,RebirthCamperTransferJournal.Record record)
    {
        var root=XElement.Parse(Encoding.UTF8.GetString(record.Before));return record.OwnerCreation==RebirthCamperCharacterAccess.CurrentCreation(player)&&
            (string)root.Attribute("world")==player.world.worldState.Guid&&(string)root.Attribute("owner")==identity.CanonicalId;
    }
    public static void Receive(long id,List<ItemStack> items,string reason,string resourceWorld,string resourceOperation,string resourceBinding,bool settled,bool noPayment,
        string gridChild,string gridHash,string ownerProof,string disposition)
    {
        if(!waiting.TryGetValue(id,out var original)||!CurrentPullSession(original))return;
        if(items==null||items.Count>short.MaxValue||items.Any(item=>item==null||item.IsEmpty()||item.count<1)||(reason?.Length??0)>4096)return;
        var operation=Operation(original.Player,original.Owner,id);var request=Requests().Read(operation);if(request==null)return;
        if(!Guid.TryParseExact(resourceOperation,"N",out var sourceOperation)||sourceOperation==Guid.Empty||resourceWorld?.Length!=64||resourceBinding?.Length!=64||
            resourceWorld.Any(value=>!Uri.IsHexDigit(value))||resourceBinding.Any(value=>!Uri.IsHexDigit(value)))return;
        if(settled)
        {
            // A terminal server result is never another owner grant. Missing
            // local original evidence cannot justify replaying an old grid.
            RetireTerminalResult(original,resourceWorld,resourceOperation,resourceBinding,gridChild,gridHash,ownerProof,disposition);return;
        }
        var root=new XElement(XElement.Parse(Encoding.UTF8.GetString(request.Before)));root.SetElementValue("items",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(items.ToArray())));root.SetAttributeValue("error",reason??"");
        root.SetAttributeValue("sourceWorld",resourceWorld);root.SetAttributeValue("sourceOperation",resourceOperation);root.SetAttributeValue("sourceBinding",resourceBinding);
        root.SetAttributeValue("noPayment",noPayment);
        byte[] bytes=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));if(bytes.Length>16*1024*1024)return;
        var result=Results().Prepare(operation,operation,original.Creation,0,bytes,bytes);
        if(result.State==RebirthCamperTransferJournal.Phase.Applied){RetireLocalResult(original);return;}
        if(result.State==RebirthCamperTransferJournal.Phase.Prepared)Results().Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
        if(noPayment&&items.Count==0&&!string.IsNullOrEmpty(reason))
        {
            AcknowledgeNoPayment(original.Player,sourceOperation,resourceBinding);
            // Keep querying the same original until its saved terminal reply;
            // lost acknowledgements cannot exhaust retained source history.
            return;
        }
        DeliverOriginalResult(original,items);
    }
    private static void AcknowledgeNoPayment(EntityPlayerLocal player,Guid operation,string binding)
    {
        if(player?.world?.IsRemote()==false)
        {RebirthRemoteResourceDurableOutcome.AcknowledgeCookingNoPayment(player,operation,binding);return;}
        var persistent=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if(persistent?.PrimaryId!=null)SingletonMonoBehaviour<ConnectionManager>.Instance?.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCookingLegacyPullNoPayment>().Setup(player.entityId,persistent.PrimaryId,operation,binding,player.world.worldState.Guid,RebirthCamperCharacterAccess.CurrentCreation(player)));
    }
    internal static bool ValidateOriginal(EntityPlayerLocal player,long id,IList<ItemStack> items)
        =>ValidateOriginalScope(player,id,items,true);
    internal static bool ValidateHistoricalReturnOriginal(EntityPlayerLocal player,long id,IList<ItemStack> items,int sourceDeaths)
    {
        if(sourceDeaths<0||player==null||sourceDeaths>=player.Died||items==null||items.Count==0||!ValidateOriginalScope(player,id,items,false)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return false;
        var root=XElement.Parse(Encoding.UTF8.GetString(Results().Read(Operation(player,identity.CanonicalId,id)).Before));
        return ReadRequestDeaths(root)==sourceDeaths&&(bool?)root.Attribute("noPayment")!=true;
    }
    private static bool ValidateOriginalScope(EntityPlayerLocal player,long id,IList<ItemStack> items,bool requireDeliveryLife)
    {
        if(player==null||items==null||id<=0||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return false;
        var record=Results().Read(Operation(player,identity.CanonicalId,id));if(record==null||record.State==RebirthCamperTransferJournal.Phase.Applied)return false;
        if(!CurrentPullSession(new Original{Player=player,World=player.world,Owner=identity.CanonicalId,Creation=record.OwnerCreation}))return false;
        var root=XElement.Parse(Encoding.UTF8.GetString(record.Before));
        return (!requireDeliveryLife||ReadRequestDeaths(root)==player.Died)&&(string)root.Attribute("world")==player.world.worldState.Guid&&(string)root.Attribute("owner")==identity.CanonicalId&&record.OwnerCreation==RebirthCamperCharacterAccess.CurrentCreation(player)&&
            root.Element("items")?.Value==Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(items.ToArray()));
    }
    internal static bool IsNoPaymentOriginal(EntityPlayerLocal player,long id)
    {
        try
        {
            if(!ValidateOriginalScope(player,id,new List<ItemStack>(),false)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return false;
            var root=XElement.Parse(Encoding.UTF8.GetString(Results().Read(Operation(player,identity.CanonicalId,id)).Before));
            return (bool?)root.Attribute("noPayment")==true&&!string.IsNullOrEmpty((string)root.Attribute("error"));
        }
        catch{return false;}
    }
    internal static void CompleteGridOriginal(EntityPlayerLocal player,long id)
    {
        if(id<=0||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return;var journal=Results();var operation=Operation(player,identity.CanonicalId,id);var record=journal.Read(operation);
        if(record?.State==RebirthCamperTransferJournal.Phase.Uncertain)journal.Advance(operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
        var requests=Requests();var request=requests.Read(operation);if(request?.State==RebirthCamperTransferJournal.Phase.Uncertain)requests.Advance(operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
    }
    internal static string EncodeCanonical(IList<ItemStack> items,int maximum)
    {
        if(items==null||items.Count>maximum||items.Any(item=>item==null||item.IsEmpty()||item.count<1))throw new InvalidDataException();
        byte[] bytes=RebirthCamperInventorySnapshot.Capture(items.ToArray());if(bytes.Length>RebirthRemoteResourceAdmission.MaximumOutcomeBytes)throw new InvalidDataException();
        return Convert.ToBase64String(bytes);
    }
    internal static List<ItemStack> DecodeCanonical(string image,int maximum)
    {
        if(image==null||image.Length>8*1024*1024)throw new InvalidDataException();byte[] bytes=Convert.FromBase64String(image);
        if(bytes.Length>RebirthRemoteResourceAdmission.MaximumOutcomeBytes)throw new InvalidDataException();var items=RebirthCamperInventorySnapshot.Restore(bytes);
        if(items.Length>maximum||items.Any(item=>item==null||item.IsEmpty()||item.count<1)||EncodeCanonical(items,maximum)!=image)throw new InvalidDataException();return items.ToList();
    }
}
[Preserve]
public sealed class NetPackageRebirthCookingLegacyPull : NetPackage
{
    private int playerId;private PlatformUserIdentifierAbs user;private long id;private List<ItemStack> needs;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthCookingLegacyPull Setup(int player,PlatformUserIdentifierAbs uid,long request,List<ItemStack> items){playerId=player;user=uid;id=request;needs=items;return this;}
    public override void read(PooledBinaryReader r){playerId=r.ReadInt32();user=PlatformUserIdentifierAbs.FromStream(r);id=r.ReadInt64();needs=RebirthCookingLegacyPull.ReadIngredientRequests(r);}
    public override void write(PooledBinaryWriter w){base.write(w);((BinaryWriter)w).Write(playerId);user.ToStream(w);((BinaryWriter)w).Write(id);GameUtils.WriteItemStack(w,needs);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        // Old peers lack original custody and canonical metadata. Reject before
        // any source withdrawal; this type remains only for explicit detection.
        if(world?.IsRemote()==false)Log.Warning("[Cooking] Legacy pull peer rejected; original-custody protocol required.");
    }
    public int GetLength()=>0;
}
[Preserve]
public sealed class NetPackageRebirthCookingLegacyPulled : NetPackage
{
    private long id;private List<ItemStack> items;private string error;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthCookingLegacyPulled Setup(long request,List<ItemStack> stacks,string reason){id=request;items=stacks;error=reason??"";return this;}
    public override void read(PooledBinaryReader r){id=r.ReadInt64();error=r.ReadString();items=new List<ItemStack>(GameUtils.ReadItemStack(r));}
    public override void write(PooledBinaryWriter w){base.write(w);((BinaryWriter)w).Write(id);((BinaryWriter)w).Write(error);GameUtils.WriteItemStack(w,items);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==true)Log.Warning("[Cooking] Legacy pull result rejected; original-custody protocol required.");}
    public int GetLength()=>0;
}

[Preserve]
public sealed class NetPackageRebirthCookingLegacyPullOriginal:NetPackage
{
    private const uint Version=0x34504F43;private int playerId,deaths;private bool queryOnly;private PlatformUserIdentifierAbs user;private long id;private List<ItemStack> needs;private string worldId,creation,packed,hash;private int total,count,index;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal static void Send(int player,PlatformUserIdentifierAbs owner,long request,List<ItemStack> items,string world,string character,int originalDeaths,bool metadataOnly)
    {
        var body=new RebirthRemoteResourceFragments.Body(new UTF8Encoding(false,true).GetBytes(RebirthCookingLegacyPull.EncodeCanonical(items,12)));
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var actor=GameManager.Instance?.World?.GetEntity(player) as EntityPlayer;
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity))return;
        var operation=RebirthCookingRemoteAdmissionBackend.StationKey(world,identity.StorageKey+"|cookingPull|"+request);
        RebirthRemoteResourceRepairProtocol.QueueCooking(false,player,operation,world,identity.CanonicalId,character,body,part=>connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCookingLegacyPullOriginal>().Setup(player,owner,request,world,character,originalDeaths,metadataOnly,body,part)));
    }
    private NetPackageRebirthCookingLegacyPullOriginal Setup(int player,PlatformUserIdentifierAbs owner,long request,string world,string character,int originalDeaths,bool metadataOnly,RebirthRemoteResourceFragments.Body body,int part)
    {playerId=player;user=owner;id=request;deaths=originalDeaths;queryOnly=metadataOnly;packed=body.Part(part);hash=body.Hash;total=body.Length;count=body.Count;index=part;worldId=world;creation=character;return this;}
    public override void read(PooledBinaryReader reader)
    {if(reader.ReadUInt32()!=Version)throw new InvalidDataException();playerId=reader.ReadInt32();user=PlatformUserIdentifierAbs.FromStream(reader);id=reader.ReadInt64();deaths=reader.ReadInt32();queryOnly=reader.ReadBoolean();worldId=RebirthCookingGridCustody.ReadWireString(reader,64);creation=RebirthCookingGridCustody.ReadWireString(reader,512);hash=RebirthCookingGridCustody.ReadWireString(reader,64);total=reader.ReadInt32();count=reader.ReadInt32();index=reader.ReadInt32();packed=RebirthCookingGridCustody.ReadWireString(reader,RebirthRemoteResourceFragments.ChunkText);}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write(Version);writer.Write(playerId);user.ToStream(writer);writer.Write(id);writer.Write(deaths);writer.Write(queryOnly);RebirthCookingGridCustody.WriteWireString(writer,worldId,64);RebirthCookingGridCustody.WriteWireString(writer,creation,512);RebirthCookingGridCustody.WriteWireString(writer,hash,64);writer.Write(total);writer.Write(count);writer.Write(index);RebirthCookingGridCustody.WriteWireString(writer,packed,RebirthRemoteResourceFragments.ChunkText);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(callbacks,GameManager.Instance)||
            manager?.IsServer!=true||world.worldState.Guid!=worldId||id<=0||
            Sender==null||!Sender.loginDone||Sender.disconnecting||!ValidEntityIdForSender(playerId)||!ValidUserIdForSender(user)||!ReferenceEquals(manager.Clients?.ForEntityId(playerId),Sender)||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(this,out var received)||received.IsDisconnected()||
            Channel<0||Sender.netConnection==null||Channel>=Sender.netConnection.Length||!ReferenceEquals(Sender.netConnection[Channel],received))return;
        var player=world.GetEntity(playerId) as EntityPlayer;if(player==null||player.IsDead()||!RebirthRemoteResourceRepairProtocol.SupportsServerActor(player)||!RebirthSurvivorRequestScope.Matches(creation,RebirthCamperCharacterAccess.CurrentCreation(player))||
            !ReferenceEquals(player.world,world)||!RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var stable)||
            !RebirthStablePlayerIdentity.TryFromClientInfo(Sender,out var authenticated)||authenticated.CanonicalId!=stable.CanonicalId||identity.CanonicalId!=stable.CanonicalId)return;
        var frame=RebirthCookingRemoteAdmissionBackend.StationKey(worldId,stable.StorageKey+"|cookingPull|"+id);
        if(total>8*1024*1024||!RebirthRemoteResourceFragments.TryAdd("cooking-request|"+worldId+"|"+stable.StorageKey+"|"+creation+"|"+deaths+"|"+queryOnly,frame,hash,total,count,index,packed,Time.realtimeSinceStartup,out var complete)||complete==null)return;
        try{needs=RebirthCookingLegacyPull.DecodeCanonical(new UTF8Encoding(false,true).GetString(complete),12);}catch{return;}
        if(needs==null||needs.Count<1)return;
        RebirthRemoteResourceDurableJournal.Record outcome;
        bool known=queryOnly?RebirthRemoteResourceDurableDispatch.TryReadCookingMetadata(player,id,needs,out outcome):
            RebirthRemoteResourceDurableDispatch.TryReadCookingReplay(player,id,needs,out outcome);
        if(!known)
        {
            // An old-life/legacy requery may inspect a retained terminal but
            // cannot create a fresh source admission after respawn or restart.
            if(queryOnly||deaths<0||deaths!=player.Died)return;
            if(needs.Any(item=>item.count>10000||!RebirthCookingCatalogue.IsIngredient(item)))return;
            if(!RebirthRemoteResourceDurableDispatch.Resolve(player,1,(ulong)id,RemoteResourceClientOperation.CookingPull,needs,false,out outcome)||outcome.Phase<2)return;
        }
        var result=outcome.Copy();var items=outcome.Phase==4?new List<ItemStack>():RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(result.Element("removed").Value)).ToList();
        NetPackageRebirthCookingLegacyPulledOriginal.Send(playerId,id,items,
            (int)result.Attribute("outcome")==1?"":result.Element("reason").Value,worldId,identity.CanonicalId,creation,outcome.World,outcome.Operation.ToString("N"),outcome.Binding,outcome.Phase==4,
            (int)result.Attribute("outcome")==2&&(result.Element("sources").Elements("source").All(source=>(int)source.Attribute("attempted")==0)||RebirthRemoteResourceDurableOutcome.IsRestoredCookingRefusal(outcome)),
            outcome.Phase==4?result.Element("ownerAction")?.ToString(SaveOptions.DisableFormatting):null,(string)result.Attribute("ownerProof"),(string)result.Attribute("disposition"));
    }
    public int GetLength()=>0;
}
[Preserve]
public sealed class NetPackageRebirthCookingLegacyPulledOriginal:NetPackage
{
    private const uint Version=0x34524F43;private long id;private List<ItemStack> items;private string error,worldId,owner,creation,packed,sourceWorld,sourceOperation,sourceBinding,hash,gridChild,gridHash,ownerProof,disposition;private int total,count,index;private bool settled,noPayment;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal static void Send(int player,long request,List<ItemStack> values,string reason,string world,string identity,string character,string resourceWorld,string operation,string binding,bool terminal,bool zeroEffect,
        string ownerAction,string proof,string terminalDisposition)
    {
        var body=new RebirthRemoteResourceFragments.Body(new UTF8Encoding(false,true).GetBytes(RebirthCookingLegacyPull.EncodeCanonical(values??new List<ItemStack>(),short.MaxValue)));
        if(!Guid.TryParseExact(operation,"N",out var original))return;
        string child="",actionHash="";
        if(terminal&&!string.IsNullOrEmpty(ownerAction))
        {
            var action=RebirthCookingRemoteAdmission.ReadProtocolXml(ownerAction,16*1024*1024);
            if(action.Name!="ownerAction"||(string)action.Attribute("domain")!="cooking-grid"||!Guid.TryParseExact((string)action.Attribute("child"),"N",out var nativeChild)||nativeChild==Guid.Empty)return;
            child=nativeChild.ToString("N");actionHash=RebirthRemoteResourceDurableJournal.Digest(ownerAction);
        }
        RebirthRemoteResourceRepairProtocol.QueueCooking(true,player,original,world,identity,character,body,part=>SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCookingLegacyPulledOriginal>().Setup(request,reason,world,identity,character,resourceWorld,operation,binding,terminal,zeroEffect,child,actionHash,proof,terminalDisposition,body,part),_attachedToEntityId:player));
    }
    private NetPackageRebirthCookingLegacyPulledOriginal Setup(long request,string reason,string world,string identity,string character,string resourceWorld,string operation,string binding,bool terminal,bool zeroEffect,
        string child,string actionHash,string proof,string terminalDisposition,RebirthRemoteResourceFragments.Body body,int part)
    {id=request;packed=body.Part(part);hash=body.Hash;total=body.Length;count=body.Count;index=part;error=reason??"";worldId=world;owner=identity;creation=character;sourceWorld=resourceWorld;sourceOperation=operation;sourceBinding=binding;settled=terminal;noPayment=zeroEffect;gridChild=child;gridHash=actionHash;ownerProof=proof??"";disposition=terminalDisposition??"";return this;}
    public override void read(PooledBinaryReader reader)
    {if(reader.ReadUInt32()!=Version)throw new InvalidDataException();id=reader.ReadInt64();worldId=RebirthCookingGridCustody.ReadWireString(reader,64);owner=RebirthCookingGridCustody.ReadWireString(reader,512);creation=RebirthCookingGridCustody.ReadWireString(reader,512);error=RebirthCookingGridCustody.ReadWireString(reader,4096);hash=RebirthCookingGridCustody.ReadWireString(reader,64);total=reader.ReadInt32();count=reader.ReadInt32();index=reader.ReadInt32();packed=RebirthCookingGridCustody.ReadWireString(reader,RebirthRemoteResourceFragments.ChunkText);sourceWorld=RebirthCookingGridCustody.ReadWireString(reader,64);sourceOperation=RebirthCookingGridCustody.ReadWireString(reader,32);sourceBinding=RebirthCookingGridCustody.ReadWireString(reader,64);settled=reader.ReadBoolean();noPayment=reader.ReadBoolean();gridChild=RebirthCookingGridCustody.ReadWireString(reader,32);gridHash=RebirthCookingGridCustody.ReadWireString(reader,64);ownerProof=RebirthCookingGridCustody.ReadWireString(reader,64);disposition=RebirthCookingGridCustody.ReadWireString(reader,32);}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write(Version);writer.Write(id);RebirthCookingGridCustody.WriteWireString(writer,worldId,64);RebirthCookingGridCustody.WriteWireString(writer,owner,512);RebirthCookingGridCustody.WriteWireString(writer,creation,512);RebirthCookingGridCustody.WriteWireString(writer,error,4096);RebirthCookingGridCustody.WriteWireString(writer,hash,64);writer.Write(total);writer.Write(count);writer.Write(index);RebirthCookingGridCustody.WriteWireString(writer,packed,RebirthRemoteResourceFragments.ChunkText);RebirthCookingGridCustody.WriteWireString(writer,sourceWorld,64);RebirthCookingGridCustody.WriteWireString(writer,sourceOperation,32);RebirthCookingGridCustody.WriteWireString(writer,sourceBinding,64);writer.Write(settled);writer.Write(noPayment);RebirthCookingGridCustody.WriteWireString(writer,gridChild,32);RebirthCookingGridCustody.WriteWireString(writer,gridHash,64);RebirthCookingGridCustody.WriteWireString(writer,ownerProof,64);RebirthCookingGridCustody.WriteWireString(writer,disposition,32);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(callbacks,GameManager.Instance)||world.worldState.Guid!=worldId||
            manager?.IsServer!=false||!manager.IsConnected||!RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(this,out var received)||received.IsDisconnected()||
            Channel<0||manager.connectionToServer==null||Channel>=manager.connectionToServer.Length||!ReferenceEquals(manager.connectionToServer[Channel],received))return;var player=world.GetPrimaryPlayer();
        if(player==null||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||identity.CanonicalId!=owner||!RebirthSurvivorRequestScope.Matches(creation,RebirthCamperCharacterAccess.CurrentCreation(player)))return;
        if(id<=0||total>8*1024*1024||!Guid.TryParseExact(sourceOperation,"N",out var source)||source==Guid.Empty)return;
        string header=RebirthRemoteResourceDurableJournal.Digest(worldId+"|"+owner+"|"+creation+"|"+id+"|"+settled+"|"+noPayment+"|"+sourceWorld+"|"+sourceBinding+"|"+error+"|"+gridChild+"|"+gridHash+"|"+ownerProof+"|"+disposition);
        string scope="cooking-result|"+identity.StorageKey+"|"+header;
        if(!RebirthRemoteResourceFragments.TryAdd(scope,source,hash,total,count,index,packed,Time.realtimeSinceStartup,out var complete)||complete==null)return;
        try{items=RebirthCookingLegacyPull.DecodeCanonical(new UTF8Encoding(false,true).GetString(complete),short.MaxValue);}catch{return;}
        RebirthCookingLegacyPull.Receive(id,items,error,sourceWorld,sourceOperation,sourceBinding,settled,noPayment,gridChild,gridHash,ownerProof,disposition);
    }
    public int GetLength()=>0;
}

[Preserve]
public sealed class NetPackageRebirthCookingLegacyPullNoPayment:NetPackage
{
    private const uint Version=0x314E5043;private int playerId;private PlatformUserIdentifierAbs user;private Guid operation;private string binding,worldId,creation;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthCookingLegacyPullNoPayment Setup(int player,PlatformUserIdentifierAbs owner,Guid original,string digest,string world,string character)
    {playerId=player;user=owner;operation=original;binding=digest;worldId=world;creation=character;return this;}
    public override void read(PooledBinaryReader reader)
    {
        if(reader.ReadUInt32()!=Version)throw new InvalidDataException();playerId=reader.ReadInt32();user=PlatformUserIdentifierAbs.FromStream(reader);
        byte[] bytes=reader.ReadBytes(16);if(bytes.Length!=16)throw new EndOfStreamException();operation=new Guid(bytes);
        binding=RebirthCookingGridCustody.ReadWireString(reader,64);worldId=RebirthCookingGridCustody.ReadWireString(reader,64);creation=RebirthCookingGridCustody.ReadWireString(reader,512);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);writer.Write(Version);writer.Write(playerId);user.ToStream(writer);writer.Write(operation.ToByteArray());
        RebirthCookingGridCustody.WriteWireString(writer,binding,64);RebirthCookingGridCustody.WriteWireString(writer,worldId,64);RebirthCookingGridCustody.WriteWireString(writer,creation,512);
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(callbacks,GameManager.Instance)||
            manager?.IsServer!=true||world.worldState.Guid!=worldId||operation==Guid.Empty||string.IsNullOrEmpty(binding)||
            !ValidEntityIdForSender(playerId)||!ValidUserIdForSender(user)||Sender?.loginDone!=true||Sender.disconnecting||
            !ReferenceEquals(manager.Clients?.ForEntityId(playerId),Sender)||!RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(this,out var received)||received.IsDisconnected()||
            Channel<0||Sender.netConnection==null||Channel>=Sender.netConnection.Length||!ReferenceEquals(Sender.netConnection[Channel],received))return;
        var player=world.GetEntity(playerId) as EntityPlayer;
        if(player==null||player.IsDead()||!ReferenceEquals(player.world,world)||!RebirthRemoteResourceRepairProtocol.SupportsServerActor(player)||
            !RebirthOriginalActionScope.TryNormalize(creation,out var normalized)||normalized!=creation||RebirthCamperCharacterAccess.CurrentCreation(player)!=creation||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var stable)||
            !RebirthStablePlayerIdentity.TryFromClientInfo(Sender,out var authenticated)||authenticated.CanonicalId!=stable.CanonicalId||identity.CanonicalId!=stable.CanonicalId)return;
        RebirthRemoteResourceDurableOutcome.AcknowledgeCookingNoPayment(player,operation,binding);
    }
    public int GetLength()=>0;
}
