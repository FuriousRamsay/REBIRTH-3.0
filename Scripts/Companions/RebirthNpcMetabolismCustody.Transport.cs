using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UnityEngine.Scripting;

internal static partial class RebirthNpcMetabolismCustody
{
    private sealed class OwnerAction
    {internal Guid Operation;internal string Purpose,Creation;internal World World;internal EntityPlayerLocal Actor;internal INetConnection Connection;internal int Deaths;internal bool Entered,Returned,SecondaryEntered,SecondaryReturned;internal IDisposable Memory;}
    private static string SecondaryReceipt(Guid operation)=>OwnerReceipt(operation)+"_secondary";
    private static string BeforeReceipt(Guid operation)=>OwnerReceipt(operation)+"_before";
    private static readonly Dictionary<Guid,OwnerAction> ownerActions=new Dictionary<Guid,OwnerAction>();
    private static void RetireOwnerAction(OwnerAction original)
    {
        ownerActions.Remove(original.Operation);original.Memory?.Dispose();original.Memory=null;
        original.World=null;original.Actor=null;original.Connection=null;original.Purpose=null;
    }
    private static void PumpOwnerObservationRetirement()
    {
        if(!ThreadManager.IsMainThread())return;
        var world=GameManager.Instance?.World;var connection=SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0];
        foreach(var original in ownerActions.Values.Where(value=>!ReferenceEquals(value.World,world)||!ReferenceEquals(value.Connection,connection)).ToArray())
            RetireOwnerAction(original); // resources only; never edit native receipt or infer consumption outcome.
    }
    private static string Purpose(Original original)
    {
        if(original.OwnerPurpose!=null)return original.OwnerPurpose;
        if(original.Text!=null)
        {
            var full=ParseOriginal(original.Text);
            return original.OwnerPurpose=new XElement("npcOwnerConsumption",new XAttribute("version",1),new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("npc",original.Id.ToString()),new XAttribute("account",(string)full.Attribute("owner")),new XAttribute("creation",(string)full.Attribute("creation")),new XAttribute("deaths",(int)full.Attribute("deaths")),
                new XElement(full.Element("source")),new XElement(full.Element("serving")),new XElement(full.Element("replacement")),new XElement(full.Element("siblings")),new XElement("nativeSecondary",full.Element("plan").Element("deferred").Elements("effect").Where(effect=>checked((byte)(int)effect.Attribute("kind"))== (byte)RebirthMetabolismService.NpcDeferredKind.HydrationCost).Select(effect=>new XElement(effect)))).ToString(SaveOptions.DisableFormatting);
        }
        return original.OwnerPurpose=new XElement("npcOwnerConsumption",new XAttribute("version",1),new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("npc",original.Id.ToString()),
            new XAttribute("account",original.Account),new XAttribute("creation",original.Creation??string.Empty),new XAttribute("deaths",original.Deaths),
            Stack("source",original.Source),Stack("serving",original.Plan.NativeItem),Stack("replacement",original.Plan.Replacement),Stack("siblings",original.Plan.Siblings),
            new XElement("nativeSecondary",original.Plan.Deferred.Where(effect=>effect.Kind==RebirthMetabolismService.NpcDeferredKind.HydrationCost).Select(effect=>effect.Write()))).ToString(SaveOptions.DisableFormatting);
    }
    private static bool nativeObserversInstalled;
    internal static void InstallNativeObservers()
    {
        if(nativeObserversInstalled)return;
        RebirthRemoteResourcePlacedStationOriginal.InstallProtocolInfrastructure();
        RebirthDogLifecycleService.InstallHireNativeObservers();InstallOwnerObservers();
        nativeObserversInstalled=true; // Publish only after all shared original sources installed.
    }
    private static void InstallOwnerObservers()
    {RebirthProductiveEarnedUiOwnerFile.Install();RebirthProductiveEarnedUiDeferredTransport.InstallSelectedSendObserver();}
    private static bool SendOwnerPurpose(Original original,byte phase)
    {
        if(!Current(original))return false;
        int courier=phase==0?0:phase==4?1:phase==1?2:-1;
        long now=System.Diagnostics.Stopwatch.GetTimestamp();if(courier>=0&&now<original.CourierAfter[courier])return false;

        string purpose=Purpose(original);if(System.Text.Encoding.UTF8.GetByteCount(purpose)>48000)return false;
        var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(original.Actor.entityId);
        if(peer==null||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner)||owner.CanonicalId!=original.Account)return false;
        if(courier>=0)original.CourierAfter[courier]=now+System.Diagnostics.Stopwatch.Frequency;
        if(phase==0)original.OwnerBeforeRequested=true;
#if REBIRTH_DEBUG
        if(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&ScenarioDropOwnerCourier?.Invoke(original.Operation,phase)==true)return true;
#endif
        peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcConsumptionOwner>().Setup(original.Actor.entityId,phase,purpose));return true;
    }
#if REBIRTH_DEBUG
    internal static Func<Guid,byte,bool> ScenarioDropOwnerCourier,ScenarioDropOwnerReturn;
    internal static Func<Guid,bool> ScenarioDropOwnerPds;
#endif
    private static bool ApplyOwnerEvent(Guid operation,EntityPlayerLocal actor,ItemValue item)
    {
        string marker=OwnerReceipt(operation);if(actor.Buffs.GetCustomVar(marker)!=0f)return false;
        actor.Buffs.SetCustomVar(marker,2f,true);
        ObserveOwnerInvoked(operation);
        if(!RebirthPreservedConsumableEffects.TryApply(actor,item,false,RebirthMetabolismService.DebugEnabled))return false;
        actor.Buffs.SetCustomVar(marker,1f,true);return true;
    }
    private static bool OwnerCurrent(OwnerAction original)=>original!=null&&ReferenceEquals(original.World,GameManager.Instance?.World)&&ReferenceEquals(original.World.GetPrimaryPlayer(),original.Actor)&&
        ReferenceEquals(original.World.GetEntity(original.Actor.entityId),original.Actor)&&!original.Actor.IsDead()&&original.Actor.Died==original.Deaths&&RebirthCamperCharacterAccess.CurrentCreation(original.Actor)==original.Creation&&
        ReferenceEquals(original.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0]);
    internal static void ReceiveOwnerPurpose(NetPackage packet,World world,int actor,byte phase,string text)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||phase>4||
                !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var received)||!ReferenceEquals(received,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0]))return;
            PumpOwnerObservationRetirement();
            var player=world.GetPrimaryPlayer();if(player==null||player.entityId!=actor||player.IsDead()||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var account))return;
            using var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=48000});var image=XElement.Load(reader);
            if(image.Name!="npcOwnerConsumption"||image.Attributes().Count()!=6||image.Elements().Count()!=5||(int?)image.Attribute("version")!=1||
                !Guid.TryParseExact((string)image.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||!RebirthNpcStableId.TryParse((string)image.Attribute("npc"),out var npc)||npc.IsEmpty||
                (string)image.Attribute("account")!=account.CanonicalId||(string)image.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(player)||phase!=2&&phase!=3&&(int?)image.Attribute("deaths")!=player.Died)return;
            if(phase==2)
            {
                bool warm=ownerActions.TryGetValue(operation,out var terminal)&&terminal.Returned&&terminal.Purpose==text&&ReferenceEquals(terminal.Actor,player)&&ReferenceEquals(terminal.World,world)&&ReferenceEquals(terminal.Connection,received)&&terminal.Creation==RebirthCamperCharacterAccess.CurrentCreation(player);
                bool cold=LoadedClientReceipt(player,operation,BeforeReceipt(operation),1f)&&LoadedClientReceipt(player,operation,OwnerReceipt(operation),1f)&&LoadedClientReceipt(player,operation,SecondaryReceipt(operation),1f);
                if(!warm&&!cold)return;
                if(terminal!=null)RetireOwnerAction(terminal);player.Buffs.RemoveCustomVar(OwnerReceipt(operation));player.Buffs.RemoveCustomVar(SecondaryReceipt(operation));player.Buffs.RemoveCustomVar(BeforeReceipt(operation));CompleteLoadedClientOperation(operation);PublishOwner(player,operation);return;
            }
            if(phase==3)
            {
                ownerActions.TryGetValue(operation,out var refused);
                bool warm=player.Buffs.GetCustomVar(OwnerReceipt(operation))==0f&&player.Buffs.GetCustomVar(SecondaryReceipt(operation))==0f&&refused!=null&&!refused.Entered&&!refused.SecondaryEntered&&refused.Purpose==text&&ReferenceEquals(refused.Actor,player)&&ReferenceEquals(refused.World,world)&&ReferenceEquals(refused.Connection,received)&&refused.Creation==RebirthCamperCharacterAccess.CurrentCreation(player);
                bool cold=LoadedClientReceipt(player,operation,BeforeReceipt(operation),1f)&&LoadedClientNoOwnerEvent(player,operation)&&LoadedClientNoSecondary(player,operation);
                if(!warm&&!cold)return;if(refused!=null)RetireOwnerAction(refused);player.Buffs.RemoveCustomVar(BeforeReceipt(operation));CompleteLoadedClientOperation(operation);PublishOwner(player,operation);return;
            }
            if(phase==0)
            {
                if(ownerActions.TryGetValue(operation,out var existing)){if(existing.Purpose!=text||!OwnerCurrent(existing))return;}
                else
                {
                    float secondary=player.Buffs.GetCustomVar(SecondaryReceipt(operation));
                    bool reconstructed=LoadedClientReceipt(player,operation,BeforeReceipt(operation),1f)&&secondary==1f&&LoadedClientReceipt(player,operation,SecondaryReceipt(operation),1f)&&LoadedClientNoOwnerEvent(player,operation);
                    if(ownerActions.Count>=4||player.Buffs.GetCustomVar(OwnerReceipt(operation))!=0f||secondary!=0f&&!reconstructed||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(1024L*1024,out var memory))return;
                    try{ownerActions.Add(operation,new OwnerAction{Operation=operation,Purpose=text,Actor=player,World=world,Connection=received,Deaths=player.Died,Creation=(string)image.Attribute("creation"),Memory=memory,SecondaryEntered=reconstructed,SecondaryReturned=reconstructed});}
                    catch{memory.Dispose();throw;}
                }
                player.Buffs.SetCustomVar(BeforeReceipt(operation),1f,true);
                PublishOwnerReturn(player,operation,text,0);PublishOwner(player,operation);return;
            }
            if(!ownerActions.TryGetValue(operation,out var action)||action.Purpose!=text||!OwnerCurrent(action))return;
            if(phase==4)
            {
                if(action.SecondaryReturned){PublishSecondaryReceipt(action);return;}
                if(action.SecondaryEntered||action.Entered||player.Buffs.GetCustomVar(SecondaryReceipt(operation))!=0f)return;
                var effects=image.Element("nativeSecondary");if(effects==null||effects.HasAttributes||effects.Elements().Count()>1)return;
                var source=ReadOriginalStack(image.Element("source"));if(source?.itemValue?.ItemClass==null)return;
                float cost=0f;foreach(var effect in effects.Elements())
                {
                    if(effect.Name!="effect"||effect.Attributes().Count()!=5||effect.HasElements||checked((byte?)(int?)effect.Attribute("kind"))!=(byte)RebirthMetabolismService.NpcDeferredKind.HydrationCost||(string)effect.Attribute("context")!=string.Empty||(bool?)effect.Attribute("drink")!=false||(string)effect.Attribute("item")!=source.itemValue.ItemClass.GetItemName())return;
                    cost=(float)effect.Attribute("value");if(float.IsNaN(cost)||float.IsInfinity(cost)||cost<0)return;
                }
                action.SecondaryEntered=true;player.Buffs.SetCustomVar(SecondaryReceipt(operation),2f,true);
                ObserveOwnerNativeCall(operation,0);
                player.Stats.Water.Value=UnityEngine.Mathf.Max(0f,player.Stats.Water.Value-cost);
                player.Buffs.SetCustomVar(SecondaryReceipt(operation),1f,true);action.SecondaryReturned=true;PublishSecondaryReceipt(action);return;
            }
            if(action.Returned){PublishOwnerReceipt(action);return;}
            if(action.Entered||!action.SecondaryReturned)return;
            var serving=image.Element("serving");if(serving==null||serving.Attributes().Count()!=2||serving.HasElements||(int?)serving.Attribute("count")!=1||
                !RebirthNativeItemCodec.TryDecode((string)serving.Attribute("item"),out var item)||item?.ItemClass==null||item.ItemClass.Actions[0] is not ItemActionConsumeMetabolismRebirth)return;
            action.Entered=true;
            if(!ApplyOwnerEvent(operation,player,item))return;action.Returned=true;PublishOwnerReceipt(action);
        }
        catch(Exception error){Log.Warning("[REBIRTH NPC] native owner consumption retained: "+error.Message);}
    }
    private static void PublishOwnerReturn(EntityPlayerLocal actor,Guid operation,string purpose,byte phase)
    {
#if REBIRTH_DEBUG
        if(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&ScenarioDropOwnerReturn?.Invoke(operation,phase)==true)return;
#endif
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthNpcConsumptionOwnerReturn>().Setup(actor.entityId,purpose,phase),false);
    }
    private static void PublishOwner(EntityPlayerLocal actor,Guid operation)
    {
#if REBIRTH_DEBUG
        if(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&ScenarioDropOwnerPds?.Invoke(operation)==true)return;
#endif
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(actor),false);
    }
    private static void PublishOwnerReceipt(OwnerAction original)
    {
        if(!OwnerCurrent(original))return;
        PublishOwnerReturn(original.Actor,original.Operation,original.Purpose,1);PublishOwner(original.Actor,original.Operation);
    }
    private static void PublishSecondaryReceipt(OwnerAction original)
    {
        if(!OwnerCurrent(original))return;
        PublishOwnerReturn(original.Actor,original.Operation,original.Purpose,2);PublishOwner(original.Actor,original.Operation);
    }
    internal static void ReceiveOwnerReturn(World world,int actor,string text,byte phase)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(world,GameManager.Instance?.World))return;
        foreach(var original in pending.Values)
            if(original.Actor.entityId==actor&&Current(original)&&Purpose(original)==text)
            {
                if(phase==0&&original.OwnerBeforeRequested&&!original.OwnerEntered){original.OwnerReady=true;return;}
                if(phase==1&&original.OwnerEntered&&!original.OwnerReturned){original.OwnerReturned=true;Record(original,"owner-effect-returned");return;}
                if(phase==2&&original.SecondaryPdsRequested&&original.DeferredReturned&&!original.OwnerEntered&&!original.OwnerSecondaryReturned){original.OwnerSecondaryReturned=true;Record(original,"owner-secondary-returned");return;}
            }
    }
}

[Preserve]public sealed class NetPackageRebirthNpcConsumptionOwner:NetPackage
{
    private int actor;private byte phase;private string purpose;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthNpcConsumptionOwner Setup(int a,byte p,string text){RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);actor=a;phase=p;purpose=text;return this;}
    public override void read(PooledBinaryReader r){RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);actor=r.ReadInt32();phase=r.ReadByte();purpose=RebirthDogLifecycleService.ReadHireWire(r);if(purpose.Length>48000)throw new InvalidDataException();}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(actor);w.Write(phase);RebirthDogLifecycleService.WriteHireWire(w,purpose);}
    public override void ProcessPackage(World world,GameManager callbacks){if(!RebirthConsumptionDurableDelivery.TryReceiveInventoryPurpose(this,world,callbacks,actor,phase,purpose))RebirthNpcMetabolismCustody.ReceiveOwnerPurpose(this,world,actor,phase,purpose);}
    public int GetLength()=>0;
}
[Preserve]public sealed class NetPackageRebirthNpcConsumptionOwnerReturn:NetPackage
{
    private int actor;private string purpose;private byte phase;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthNpcConsumptionOwnerReturn Setup(int a,string text,byte p){RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);actor=a;purpose=text;phase=p;return this;}
    public override void read(PooledBinaryReader r){RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);actor=r.ReadInt32();phase=r.ReadByte();purpose=RebirthDogLifecycleService.ReadHireWire(r);if(purpose.Length>48000||phase>2)throw new InvalidDataException();}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(actor);w.Write(phase);RebirthDogLifecycleService.WriteHireWire(w,purpose);}
    public override void ProcessPackage(World world,GameManager callbacks){if(ValidEntityIdForSender(actor)&&!RebirthConsumptionDurableDelivery.TryReceiveInventoryRefusal(this,world,callbacks,actor,purpose,phase))RebirthNpcMetabolismCustody.ReceiveOwnerReturn(world,actor,purpose,phase);}
    public int GetLength()=>0;
}
