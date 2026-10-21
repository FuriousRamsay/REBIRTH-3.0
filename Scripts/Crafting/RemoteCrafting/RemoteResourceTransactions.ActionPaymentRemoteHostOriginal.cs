using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    internal sealed class ActionRemoteHostOriginal
    {
        internal Guid Operation;internal World World;internal EntityPlayer Actor;internal ClientInfo Peer;internal INetConnection Connection;
        internal TileEntityPowered Target;internal Vector3i Position;internal string Account,Creation,Save,Click,Path,Directory,NativeWorld;internal int Deaths;
        internal ItemStack[] BagBefore,BeltBefore,Price;internal byte[] NativeBefore;
        internal PlayerDataFile Reading,AcceptedData;internal bool Accepted,Saved,Confirmed,Retired;internal int Workers;internal IDisposable Memory;
        internal ulong Recovery;internal long AdmissionNext;internal bool AdmissionIssued;
        internal string AdmissionStage;internal bool AdmissionEntered;
#if REBIRTH_DEBUG
        internal bool AdmissionScenarioPaused;
#endif
        internal ActionHostOwnerLoad RestartNativeSource;
        internal string NoAdmission,NoAdmissionReason;internal bool NoAdmissionIssued,TerminalAccepted,TerminalSaved;internal PlayerDataFile TerminalReading,TerminalData;internal byte[] NativeTerminal;
        internal long ProjectionGraphBytes;
        internal RebirthRemoteResourceFragments.Body RefusalBody;internal IDisposable RefusalMemory;internal long RefusalNext;
        internal bool MetadataCurrent()=>!Retired&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&World?.IsRemote()==false&&
            ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&!Actor.IsDead()&&Actor.Died>=Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(Actor.entityId),Peer)&&ReferenceEquals(Peer.netConnection?[0],Connection)&&
            RebirthStablePlayerIdentity.TryFromClientInfo(Peer,out var owner)&&owner.CanonicalId==Account&&Save==System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())&&World.worldState.Guid==NativeWorld;
        internal bool Current()=>!Retired&&(RestartNativeSource==null||ActionHostLoadedCurrent(RestartNativeSource))&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&World?.IsRemote()==false&&
            ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&!Actor.IsDead()&&Actor.Died==Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(Actor.entityId),Peer)&&ReferenceEquals(Peer.netConnection?[0],Connection)&&
            RebirthStablePlayerIdentity.TryFromClientInfo(Peer,out var owner)&&owner.CanonicalId==Account&&ReferenceEquals(World.GetTileEntity(Position),Target)&&
            Save==System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())&&World.worldState.Guid==NativeWorld&&Recovery==RebirthNpcRecoveryGeneration.RequireCurrent();
    }
    private static readonly Dictionary<Guid,ActionRemoteHostOriginal> actionRemoteHostOriginals=new Dictionary<Guid,ActionRemoteHostOriginal>();
    [ThreadStatic]private static ActionRemoteHostOriginal actionRemoteAdmission;
    private static bool RemoteActionOriginalCurrent(ActionRemoteHostOriginal original)=>original!=null&&original.Current()&&original.Accepted&&original.Saved&&original.Confirmed&&
        actionRemoteHostOriginals.TryGetValue(original.Operation,out var registered)&&ReferenceEquals(registered,original);
    private static bool ConfirmRemoteActionBefore(ActionRemoteHostOriginal pending)
    {
        if(pending==null||!pending.Current()||!pending.Accepted||!pending.Saved)return false;
        if(pending.Confirmed)return true;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var scratch))return false;
        using(scratch)
        {
            if(!ActionBytes(System.IO.Path.Combine(pending.Directory,"before.native"),pending.NativeBefore.Length).SequenceEqual(pending.NativeBefore))return false;
            byte[] body=EncodeActionRemoteEnvelope(pending.Click,pending.NativeBefore);string path=System.IO.Path.Combine(pending.Directory,"confirmed.native");ActionPath(path);
            if(!File.Exists(path)){using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(body,0,body.Length);output.Flush(true);}}
            if(!ActionEnvelopeFileEquals(path,body))return false;
            pending.Confirmed=true;
            // The exact full accepted+saved image and durable confirmation now
            // own this historical evidence. No native PlayerDataFile graph is
            // required after its actual acceptance/save callbacks completed.
            if(pending.Workers==0&&pending.Reading==null)pending.AcceptedData=null;
            return true;
        }
    }
    private static void RetireReplacedActionRemoteHosts()
    {
        if(!ThreadManager.IsMainThread())return;
        lock(actionOwnerGate)foreach(var pending in actionRemoteHostOriginals.Values.ToArray())
        {
            if(!pending.Retired&&(!ReferenceEquals(pending.World,GameManager.Instance?.World)||pending.World.worldState.Guid!=pending.NativeWorld||!ReferenceEquals(pending.World.GetEntity(pending.Actor.entityId),pending.Actor)||
                !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(pending.Actor.entityId),pending.Peer)||!ReferenceEquals(pending.Peer.netConnection?[0],pending.Connection)))pending.Retired=true;
            if(pending.Retired&&pending.Workers==0)DropRetiredActionRemoteHost(pending);
        }
    }
    private static void DropRetiredActionRemoteHost(ActionRemoteHostOriginal pending)
    {
        if(!pending.Retired||pending.Workers!=0)return;
        var loaded=pending.RestartNativeSource;pending.RestartNativeSource=null;
        if(loaded!=null&&!actionRemoteHostOriginals.Values.Any(value=>!ReferenceEquals(value,pending)&&ReferenceEquals(value.RestartNativeSource,loaded)))DropActionHostOwnerLoad(loaded);
        actionRemoteHostOriginals.Remove(pending.Operation);
        pending.World=null;pending.Actor=null;pending.Peer=null;pending.Connection=null;pending.Target=null;pending.Reading=null;pending.AcceptedData=null;
        pending.NativeBefore=null;pending.BagBefore=null;pending.BeltBefore=null;pending.Price=null;pending.Click=null;
        pending.TerminalReading=null;pending.TerminalData=null;pending.NativeTerminal=null;pending.NoAdmission=null;pending.RefusalBody=null;pending.RefusalMemory?.Dispose();pending.RefusalMemory=null;
        pending.Memory?.Dispose();pending.Memory=null;
    }
    // Called only after the purpose package checks its actual Sender and final
    // complete fragment body. Described bytes select a candidate, not authority.
    private static bool RegisterActionRemoteHostOriginal(World world,EntityPlayer actor,ClientInfo peer,Guid operation,byte[] envelope)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||actor==null||actor.IsDead()||peer==null||operation==Guid.Empty||
            !ReferenceEquals(world.GetEntity(actor.entityId),actor)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId),peer)||
            !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var account)||peer.netConnection?[0]==null)return false;
        lock(actionOwnerGate)
        {
            if(actionRemoteHostOriginals.TryGetValue(operation,out var known))return known.Current()&&ReferenceEquals(known.Actor,actor)&&ReferenceEquals(known.Peer,peer);
            if(actionRemoteHostOriginals.Count>=4||actionRemoteHostOriginals.Values.Any(value=>!value.Retired&&ReferenceEquals(value.Actor,actor))||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out var memory))return false;
            ActionRemoteHostOriginal pending=null;
            try
            {
                ReadActionRemoteEnvelope(envelope,out string text,out _,false);var image=ReadRemoteActionOriginal(text);
                if(image.Name!="electricalOwnerClick"||(int?)image.Attribute("version")!=1||(string)image.Attribute("operation")!=operation.ToString("N")||
                    (string)image.Attribute("world")!=world.worldState.Guid||(int?)image.Attribute("nativeId")!=actor.entityId||(string)image.Attribute("account")!=account.CanonicalId||(string)image.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||(int?)image.Attribute("deaths")!=actor.Died)return false;
                byte[] bagFrame=RemoteActionFrameBytes(image.Element("bag")),beltFrame=RemoteActionFrameBytes(image.Element("belt")),priceFrame=RemoteActionFrameBytes(image.Element("price"));
                long graph=checked(MeasureActionRetainedFrame(bagFrame)+MeasureActionRetainedFrame(beltFrame)+MeasureActionRetainedFrame(priceFrame));
                long preparing=checked(graph+16L*(bagFrame.Length+beltFrame.Length+priceFrame.Length)+2L*text.Length+12L*1024*1024);
                if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,preparing))return false;
                var position=new Vector3i((int)image.Attribute("x"),(int)image.Attribute("y"),(int)image.Attribute("z"));
                if(world.GetTileEntity(position) is not TileEntityPowered target||!target.IsPlayerPlaced||(int?)image.Attribute("block")!=world.GetBlock(position).type||
                    (actor.position-position.ToVector3()).sqrMagnitude>Constants.cDigAndBuildDistance*Constants.cDigAndBuildDistance||
                    !RebirthInfrastructureWorkService.TryDescribeActionPrice(world,target,position,out var price)||!SameActionItems(RebirthCamperInventorySnapshot.Restore(priceFrame),price))return false;
                pending=new ActionRemoteHostOriginal{Operation=operation,World=world,Actor=actor,Peer=peer,Connection=peer.netConnection[0],Target=target,Position=position,
                    Account=account.CanonicalId,Creation=RebirthCamperCharacterAccess.CurrentCreation(actor),Deaths=actor.Died,Save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir()),NativeWorld=world.worldState.Guid,
                    Click=text,BagBefore=RebirthCamperInventorySnapshot.Restore(bagFrame),BeltBefore=RebirthCamperInventorySnapshot.Restore(beltFrame),Price=price,Memory=memory,Recovery=RebirthNpcRecoveryGeneration.RequireCurrent()};
                bagFrame=null;beltFrame=null;priceFrame=null;
                pending.Path=System.IO.Path.GetFullPath(System.IO.Path.Combine(GameIO.GetPlayerDataDir(),pending.Account+"."+PlayerDataFile.EXT));
                if(!pending.Current()||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(pending.Path))return false;
                pending.Directory=System.IO.Path.Combine(pending.Save,"RebirthData","ActionOwnerOriginals",operation.ToString("N"));
                ActionPath(pending.Directory);
                // A prior process's original requires native reconstruction.
                // An authenticated retry is not a new first-grant source, even
                // when its described Before happens to equal current items.
                string paymentDirectory=System.IO.Path.Combine(pending.Save,"RebirthData","ActionPayments",operation.ToString("N"));ActionPath(paymentDirectory);
                if(System.IO.Directory.Exists(pending.Directory)||File.Exists(pending.Directory)||System.IO.Directory.Exists(paymentDirectory)||File.Exists(paymentDirectory))return false;
                System.IO.Directory.CreateDirectory(pending.Directory);
                // This is an immutable proposal/lifecycle record only; native
                // acceptance and full saved Before are issued by later events.
                byte[] record=new System.Text.UTF8Encoding(false,true).GetBytes(new XElement("electricalOwnerNativeOriginal",new XAttribute("version",1),
                    new XAttribute("world",world.worldState.Guid),new XAttribute("save",pending.Save),new XAttribute("recovery",pending.Recovery),new XAttribute("account",pending.Account),
                    new XAttribute("creation",pending.Creation),new XAttribute("deaths",pending.Deaths),new XAttribute("operation",operation.ToString("N")),
                    new XElement("click",pending.Click)).ToString(SaveOptions.DisableFormatting));
                string archive=System.IO.Path.Combine(pending.Directory,"original.xml");
                if(File.Exists(archive)){if(!ActionBytes(archive,record.Length).SequenceEqual(record))return false;}
                else{using(var output=new FileStream(archive,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(record,0,record.Length);output.Flush(true);}if(!ActionBytes(archive,record.Length).SequenceEqual(record))return false;}
                if(!NarrowActionRemoteHostMemory(pending))return false;
                actionRemoteHostOriginals.Add(operation,pending);memory=null;return true;
            }
            catch{return false;}
            finally{memory?.Dispose();}
        }
    }
    private sealed class ActionRemoteBeforeSave
    {internal ActionRemoteHostOriginal Original;internal string Path;internal byte[] Native;internal bool Finished,Terminal;}
    private static ActionRemoteBeforeSave EnterRemoteActionBeforeSave(PlayerDataFile data,string directory,string name,bool ran)
    {
        if(!ran||data==null)return null;
        lock(actionOwnerGate)
        {
            foreach(var pending in actionRemoteHostOriginals.Values)
            {
                if(!pending.Retired&&pending.NoAdmission!=null&&!pending.TerminalSaved&&pending.Workers==0&&
                    (ReferenceEquals(pending.TerminalReading,data)||ReferenceEquals(pending.TerminalData,data)))
                {
                    string terminalPath=System.IO.Path.GetFullPath(System.IO.Path.Combine(directory,name+"."+PlayerDataFile.EXT));
                    if(terminalPath!=pending.Path||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(terminalPath)||!pending.NativeTerminal.SequenceEqual(ActionOwnerImage(data)))return null;
                    pending.Workers++;return new ActionRemoteBeforeSave{Original=pending,Path=terminalPath,Native=pending.NativeTerminal,Terminal=true};
                }
                if(pending.Retired||pending.Saved||pending.Workers!=0||!ReferenceEquals(pending.Reading,data)&&!ReferenceEquals(pending.AcceptedData,data))continue;
                string path=System.IO.Path.GetFullPath(System.IO.Path.Combine(directory,name+"."+PlayerDataFile.EXT));
                if(path!=pending.Path||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path)||!pending.NativeBefore.SequenceEqual(ActionOwnerImage(data)))return null;
                var save=new ActionRemoteBeforeSave{Original=pending,Path=path,Native=pending.NativeBefore};pending.Workers++;return save;
            }
            return null;
        }
    }
    private static void ObserveRemoteActionBeforeSave(bool ran,ActionRemoteBeforeSave save)
    {
        lock(actionOwnerGate)
        {
            if(save.Finished)return;save.Finished=true;var pending=save.Original;
            try
            {
                // Worker-only immutable file scope: never touch World, peer,
                // player, Unity or current identity from this callback.
                if(!ran||pending.Retired||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(save.Path,save.Native))return;
                string archive=System.IO.Path.Combine(pending.Directory,save.Terminal?"terminal.native":"before.native");ActionPath(archive);
                if(File.Exists(archive)){if(!ActionBytes(archive,save.Native.Length).SequenceEqual(save.Native))return;}
                else{using(var output=new FileStream(archive,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(save.Native,0,save.Native.Length);output.Flush(true);}if(!ActionBytes(archive,save.Native.Length).SequenceEqual(save.Native))return;}
                if(save.Terminal){pending.TerminalSaved=true;if(pending.TerminalAccepted)pending.TerminalData=null;}
                else{pending.Saved=true;if(pending.Accepted)pending.AcceptedData=null;}
            }
            catch{}
            finally{pending.Workers--;save.Original=null;save.Native=null;if(pending.Retired)DropRetiredActionRemoteHost(pending);}
        }
    }
    private static ActionRemoteHostOriginal EnterAcceptedRemoteActionOriginal(ClientInfo peer,PlayerDataFile data,bool ran)
    {
        if(!ran||!ThreadManager.IsMainThread()||data==null||peer==null||data.id!=peer.entityId)return null;
        foreach(var pending in actionRemoteHostOriginals.Values)
        {
            if(pending.NoAdmission!=null&&pending.MetadataCurrent()&&ReferenceEquals(pending.Peer,peer)&&data.id==pending.Actor.entityId&&!pending.TerminalSaved&&pending.Workers==0&&data.deaths>=pending.Deaths&&!data.bDead&&
                RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,ActionOwnerMarker(pending.Operation),out float terminalPhase,out bool terminalPresent)&&terminalPresent&&terminalPhase==6f)
            {
                if(!CaptureActionRemoteHostImage(pending,data,true))return null;
                pending.TerminalAccepted=false;pending.TerminalData=null;pending.TerminalReading=data;return pending;
            }
            if(!ReferenceEquals(pending.Peer,peer)||!pending.Current()||pending.Saved||pending.Workers!=0||data.id!=pending.Actor.entityId||data.deaths!=pending.Deaths||data.bDead||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,ActionOwnerMarker(pending.Operation),out float phase,out bool present)||!present||phase!=1f)continue;
            if(data.dragAndDropItem==null||!data.dragAndDropItem.IsEmpty()||data.bagData==null||data.inventoryData==null||data.bagData.Length+data.inventoryData.Length>4L*1024*1024)return null;
            if(!TryReserveActionOwnerProjection(data,out var scratch))return null;
            using(scratch)
            {
                if(!SameActionItems(RebirthGearPlayerDataInventory.ReadSlots(data,true),pending.BagBefore)||!SameActionItems(RebirthGearPlayerDataInventory.ReadSlots(data,false),pending.BeltBefore))return null;
                // The described request body is never a native file issuer.
                // Capture the full ACTUAL currently accepted upload here; normal
                // vitals/time may have changed since the original click without
                // changing any financial source row or original lifecycle.
                // A failed native Save may be followed by a legitimate fresh
                // progression/autosave head. Supersede only an UNSAVED
                // observation, after validating every financial row above.
                // Never upload/reinstall the prior frozen PDS or overwrite a
                // successfully saved immutable Before. Acceptance must be
                // witnessed again on this exact new actual native object.
                if(!CaptureActionRemoteHostImage(pending,data,false))return null;
                pending.Accepted=false;pending.AcceptedData=null;pending.Reading=data;return pending;
            }
        }
        return null;
    }
    private static void ObserveAcceptedRemoteActionOriginal(ClientInfo peer,PlayerDataFile data,bool ran,ActionRemoteHostOriginal pending)
    {
        if(pending==null)return;
        try
        {
            if(ReferenceEquals(data,pending.TerminalReading))
            {
                if(ran&&pending.MetadataCurrent()&&ReferenceEquals(peer,pending.Peer)&&ReferenceEquals(peer.latestPlayerData,data)&&pending.NativeTerminal.SequenceEqual(ActionOwnerImage(data)))
                {pending.TerminalAccepted=true;pending.TerminalData=pending.TerminalSaved?null:data;}
                return;
            }
            if(ran&&pending.Current()&&ReferenceEquals(peer,pending.Peer)&&ReferenceEquals(data,pending.Reading)&&ReferenceEquals(peer.latestPlayerData,data)&&
                pending.NativeBefore.SequenceEqual(ActionOwnerImage(data))){pending.Accepted=true;pending.AcceptedData=pending.Saved?null:data;}
        }
        finally{pending.Reading=null;pending.TerminalReading=null;}
    }
    private static void RetireSupersededActionOwnerData(ClientInfo peer,PlayerDataFile data,bool ran)
    {
        if(!ran||!ThreadManager.IsMainThread()||peer==null||data==null||data.id!=peer.entityId||!ReferenceEquals(peer.latestPlayerData,data))return;
        lock(actionOwnerGate)foreach(var source in actionRemoteHostOriginals.Values)
        {
            if(!ReferenceEquals(source.Peer,peer)||source.Retired||!ReferenceEquals(GameManager.Instance?.World,source.World)||
                !ReferenceEquals(source.Connection,peer.netConnection?[0]))continue;
            // Actual accepted successor supersedes only observation references.
            // Full immutable historical images/accepted/saved facts remain.
            // A worker already owns its own immutable Native byte[] scope.
            if(source.AcceptedData!=null&&!ReferenceEquals(source.AcceptedData,data))source.AcceptedData=null;
            if(source.TerminalData!=null&&!ReferenceEquals(source.TerminalData,data))source.TerminalData=null;
        }
    }
}
