using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthConsumptionDurableDelivery
{
    private sealed class InventoryClient
    {
        internal Guid Operation;internal EntityPlayerLocal Actor;internal World World;internal ConnectionManager Manager;internal INetConnection Connection;
        internal string Creation,Text,Path;internal int Deaths;internal RebirthConsumptionInventoryPlan Plan;internal ItemValue Serving;
        internal float HydrationCost;internal IDisposable Memory;internal bool Local;internal RebirthConsumeResult Result;
        internal bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&
            ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&!Actor.IsDead()&&Actor.Died==Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,Manager)&&(Local?Manager.IsServer:
                !Manager.IsServer&&Manager.connectionToServer?.Length>0&&ReferenceEquals(Manager.connectionToServer[0],Connection)&&!Connection.IsDisconnected());
    }
    private sealed class InventoryPermit{internal InventoryClient Client;internal ItemStackGrid Grid;internal int Slot;internal ItemStack Value;internal bool Consumed;}
    private static readonly Dictionary<Guid,InventoryClient> inventoryClients=new Dictionary<Guid,InventoryClient>();
    [ThreadStatic]private static InventoryPermit inventoryPermit;
    [ThreadStatic]private static InventoryOriginal inventoryLocalAdmission;
    internal static bool HoldsInventoryOwner(EntityPlayerLocal actor)=>!ReferenceEquals(inventoryLocalAdmission?.Actor,actor)&&HoldsLoadedInventoryActor(actor)||
        inventoryClients.Values.Any(value=>ReferenceEquals(value.Actor,actor)&&value.Current());
    internal static bool ConsumeInventorySlot(ItemStackGrid grid,int slot,ItemStack value)
    {
        var permit=inventoryPermit;
        if(permit==null||permit.Consumed||!permit.Client.Current()||!ReferenceEquals(permit.Grid,grid)||permit.Slot!=slot||!ReferenceEquals(permit.Value,value))return false;
        permit.Consumed=true;return true;
    }
    private static ItemStack PurposeStack(XElement row)
    {
        if(row==null||row.Attributes().Count()!=1||row.HasElements||(int?)row.Attribute("count") is not int count||count<0||count>ushort.MaxValue)throw new InvalidDataException();
        if(count==0){if(row.Value!="")throw new InvalidDataException();return ItemStack.Empty.Clone();}
        if(!RebirthNativeItemCodec.TryDecode(row.Value,out var item)||item?.ItemClass==null)return null;
        return new ItemStack(item,count);
    }
    private static ItemStack[] PurposeInventory(XElement row)
    {
        if(row==null||row.HasAttributes||row.HasElements||row.Value.Length>MaximumInventoryPurpose)throw new InvalidDataException();
        var bytes=Convert.FromBase64String(row.Value);var result=RebirthCamperInventorySnapshot.Restore(bytes);
        if(Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(result))!=row.Value)throw new InvalidDataException();return result;
    }
    private static bool TryReadInventoryPurpose(EntityPlayerLocal player,string text,out Guid operation,out RebirthConsumptionInventoryPlan plan,out ItemValue serving,out float cost,out RebirthConsumeResult result,bool metadataOnly=false)
    {
        operation=Guid.Empty;plan=null;serving=null;cost=0f;result=default;
        if(text==null||Encoding.UTF8.GetByteCount(text)>MaximumInventoryPurpose)return false;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(192L*1024*1024,out var scratch))return false;
        using var decoderScratch=scratch;
        using var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumInventoryPurpose});
        var root=XElement.Load(reader);
        string[] members={"beforeBag","beforeBelt","afterBag","afterBelt","bagLocks","beltLocks","source","replacement","siblings","serving","sourceAddress","nativeSecondary","feedback"};
        if(root.Name!="playerOwnerConsumption"||root.Attributes().Count()!=7||(int?)root.Attribute("version")!=1||root.Elements().Count()!=members.Length||
            members.Any(name=>root.Elements(name).Count()!=1)||!Guid.TryParseExact((string)root.Attribute("operation"),"N",out operation)||operation==Guid.Empty||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var account)||(string)root.Attribute("account")!=account.CanonicalId||
            (string)root.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(player)||
            (metadataOnly?((int?)root.Attribute("deaths") is not int historical||historical<0||historical>player.Died):(int?)root.Attribute("deaths")!=player.Died)||
            (string)root.Attribute("world")!=player.world.worldState.Guid||((string)root.Attribute("binding"))?.Length!=64)return false;
        var bag=PurposeInventory(root.Element("beforeBag"));var belt=PurposeInventory(root.Element("beforeBelt"));
        var afterBag=PurposeInventory(root.Element("afterBag"));var afterBelt=PurposeInventory(root.Element("afterBelt"));
        var address=root.Element("sourceAddress");if(address.HasElements||address.Attributes().Count()!=3)return false;
        var kind=(RebirthMetabolismSourceKind)checked((byte)(int)address.Attribute("kind"));int slot=(int)address.Attribute("slot"),width=(int)address.Attribute("beltCount");
        bool[] Locks(string name,int count)
        {var row=root.Element(name);if(row.HasAttributes||row.HasElements||row.Value.Length!=count||row.Value.Any(value=>value!='0'&&value!='1'))throw new InvalidDataException();return row.Value.Select(value=>value=='1').ToArray();}
        var source=PurposeStack(root.Element("source"));var replacement=PurposeStack(root.Element("replacement"));var siblings=PurposeStack(root.Element("siblings"));
        if(!RebirthConsumptionInventoryPlan.TryCreate(kind,slot,width,bag,belt,Locks("bagLocks",bag.Length),Locks("beltLocks",belt.Length),source,replacement,siblings,out plan)||
            !plan.MatchesAfter(afterBag,afterBelt)||!metadataOnly&&width!=RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length)||
            root.Element("serving").HasAttributes||root.Element("serving").HasElements||!RebirthNativeItemCodec.TryDecode(root.Element("serving").Value,out serving)||
            serving?.ItemClass?.Actions[0] is not ItemActionConsumeMetabolismRebirth)return false;
        var secondary=root.Element("nativeSecondary");if(secondary.HasAttributes||secondary.Elements().Count()>1)return false;
        foreach(var effect in secondary.Elements())
        {
            if(effect.Name!="effect"||effect.Attributes().Count()!=5||effect.HasElements||checked((byte?)(int?)effect.Attribute("kind"))!=(byte)RebirthMetabolismService.NpcDeferredKind.HydrationCost||
                (string)effect.Attribute("context")!=""||(bool?)effect.Attribute("drink")!=false||(string)effect.Attribute("item")!=source.itemValue.ItemClass.GetItemName())return false;
            cost=(float)effect.Attribute("value");if(float.IsNaN(cost)||float.IsInfinity(cost)||cost<0)return false;
        }
        var feedback=root.Element("feedback");var message=feedback.Element("result");
        if(feedback.HasAttributes||feedback.Elements().Count()!=1||message==null||message.HasElements||message.Attributes().Count()!=4||message.Value.Length>1024)return false;
        float consumed=(float)message.Attribute("consumed"),remaining=(float)message.Attribute("remaining");
        if(float.IsNaN(consumed)||float.IsInfinity(consumed)||consumed<0||float.IsNaN(remaining)||float.IsInfinity(remaining)||remaining<0)return false;
        result=new RebirthConsumeResult{Success=true,Failure=RebirthMetabolismConsumeFailure.None,ConsumedMl=consumed,RemainingMl=remaining,AddedEntryId=(int)message.Attribute("entry"),CreatedEmptyItem=(bool)message.Attribute("empty"),Message=message.Value};
        return true;
    }
    private static void PublishInventoryOwner(InventoryClient client)
    {
        if(!client.Current())return;GameManager.Instance.SaveLocalPlayerData();
        if(!client.Current())return;
        if(!client.Local)client.Manager.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(client.Actor),false);
    }
    private static RebirthCamperTransferJournal InventoryClientJournal(InventoryClient client)=>new RebirthCamperTransferJournal(client.Path);
    private static Guid InventoryCommandKey(Guid operation,int ordinal)=>Guid.ParseExact(RebirthRemoteResourceDurableJournal.Digest(operation.ToString("N")+"|source|"+ordinal).Substring(0,32),"N");
    internal static bool TryReceiveInventoryPurpose(NetPackage packet,World world,GameManager callbacks,int actor,byte phase,string text)
        =>ApplyInventoryPurpose(null,packet,world,callbacks,actor,phase,text);
    private static bool ApplyInventoryPurpose(InventoryOriginal local,NetPackage packet,World world,GameManager callbacks,int actor,byte phase,string text)
    {
        if(local==null&&TryReceiveInventoryPurposeFragment(packet,world,callbacks,actor,phase,text,false,out string assembled))
        {if(assembled==null)return true;text=assembled;}
        if(text==null||!text.StartsWith("<playerOwnerConsumption",StringComparison.Ordinal))return false;
        InventoryClient preparing=null;
        try
        {
            INetConnection connection=null;
            if(local==null)
            {if(!RebirthRemoteResourceNetworkScope.Receive(packet,world,callbacks,false,out connection))return true;}
            else if(!local.Current()||!ReferenceEquals(local.Actor,world.GetPrimaryPlayer())||local.Purpose!=text||!ReferenceEquals(callbacks,GameManager.Instance))return true;
            var player=world.GetPrimaryPlayer();if(player==null||player.entityId!=actor||!TryReadInventoryPurpose(player,text,out var operation,out var plan,out var serving,out float cost,out var result,phase==3))return true;
            if(!inventoryClients.TryGetValue(operation,out var client))
            {
                if(phase==0&&(!plan.MatchesBefore(player.bag.ItemGrid.items,player.inventory.ItemGrid.items)||!plan.MatchesLocks(player)))
                {
                    if(local!=null)RefuseUnissuedInventoryOriginal(local);
                    else if(Encoding.UTF8.GetByteCount(text)>48000)QueueInventoryRefusal(player,operation,text);
                    else SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthNpcConsumptionOwnerReturn>().Setup(actor,text,0),false);
                    return true;
                }
                if(phase!=0||inventoryClients.Count>=4||RebirthCookingOutputDelivery.IsHeld(player)||!plan.MatchesBefore(player.bag.ItemGrid.items,player.inventory.ItemGrid.items)||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(192L*1024*1024,out var memory))return true;
                client=new InventoryClient{Operation=operation,Actor=player,World=world,Manager=SingletonMonoBehaviour<ConnectionManager>.Instance,Connection=connection,
                    Creation=RebirthCamperCharacterAccess.CurrentCreation(player),Deaths=player.Died,Text=text,Plan=plan,Serving=serving,HydrationCost=cost,Result=result,Memory=memory,Local=local!=null,
                    Path=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","PlayerConsumptionClient",operation.ToString("N"))};
                preparing=client;
                byte[] bytes=Encoding.UTF8.GetBytes(text);var saved=InventoryClientJournal(client).Prepare(operation,operation,client.Creation,0,bytes,bytes);
                if(saved.State!=RebirthCamperTransferJournal.Phase.Prepared||!saved.Before.SequenceEqual(bytes)||!saved.After.SequenceEqual(bytes))return true;
                var initialCommands=plan.Commands();
                for(int ordinal=0;ordinal<initialCommands.Length;ordinal++)
                {
                    var command=initialCommands[ordinal];var key=InventoryCommandKey(operation,ordinal);
                    var before=RebirthCamperInventorySnapshot.Capture(new[]{command.Before});var after=RebirthCamperInventorySnapshot.Capture(new[]{command.After});
                    var record=InventoryClientJournal(client).Prepare(key,operation,client.Creation,ordinal,before,after);
                    if(record.State!=RebirthCamperTransferJournal.Phase.Prepared||!record.Before.SequenceEqual(before)||!record.After.SequenceEqual(after))throw new InvalidDataException("Player consumption command original conflicts.");
                }
                inventoryClients.Add(operation,client);
                ObserveInventoryAdmission(operation,player,text);
                preparing=null;
            }
            if(client.Text!=text||!client.Current()||!ReferenceEquals(client.Connection,connection))return true;
            if(phase==3&&player.Buffs.GetCustomVar(InventorySourceReceipt(operation))==3f)
            {
                var journal=InventoryClientJournal(client);var record=journal.Read(operation);
                bool notify=record.State!=RebirthCamperTransferJournal.Phase.Applied;
                if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
                if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
                if(record.State!=RebirthCamperTransferJournal.Phase.Applied)return true;
                inventoryClients.Remove(operation);client.Memory.Dispose();player.Buffs.RemoveCustomVar(InventorySourceReceipt(operation));PublishInventoryOwner(client);
                RebirthNpcMetabolismCustody.CompletePlayerNativeConsumption(operation);if(notify)RebirthMetabolismUiFeedback.Receive(client.Result);return true;
            }
            if(phase==2)
            {
                if(player.Buffs.GetCustomVar(InventorySourceReceipt(operation))==3f){PublishInventoryOwner(client);return true;}
                if(player.Buffs.GetCustomVar(InventorySourceReceipt(operation))!=1f||player.Buffs.GetCustomVar(InventorySecondaryReceipt(operation))!=1f||player.Buffs.GetCustomVar(InventoryUseReceipt(operation))!=1f)return true;
                player.Buffs.SetCustomVar(InventorySourceReceipt(operation),3f,true);player.Buffs.RemoveCustomVar(InventoryBeforeReceipt(operation));
                player.Buffs.RemoveCustomVar(InventorySecondaryReceipt(operation));player.Buffs.RemoveCustomVar(InventoryUseReceipt(operation));PublishInventoryOwner(client);return true;
            }
            if(phase==0)
            {
                if(player.Buffs.GetCustomVar(InventorySourceReceipt(operation))!=0f||!client.Plan.MatchesBefore(player.bag.ItemGrid.items,player.inventory.ItemGrid.items)||!client.Plan.MatchesLocks(player))return true;
                player.Buffs.SetCustomVar(InventoryBeforeReceipt(operation),1f,true);PublishInventoryOwner(client);return true;
            }
            var commands=client.Plan.Commands();
            if(phase>=10&&phase-10<commands.Length)
            {
                int ordinal=phase-10;float marker=player.Buffs.GetCustomVar(InventorySourceReceipt(operation));
                if(marker==100f+ordinal+1&&client.Plan.MatchesPrefix(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,ordinal+1)){PublishInventoryOwner(client);return true;}
                if((ordinal==0?marker!=0f:marker!=100f+ordinal)||!client.Plan.MatchesPrefix(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,ordinal)||!client.Plan.MatchesLocks(player))return true;
                var command=commands[ordinal];var key=InventoryCommandKey(operation,ordinal);var journal=InventoryClientJournal(client);
                byte[] before=RebirthCamperInventorySnapshot.Capture(new[]{command.Before}),after=RebirthCamperInventorySnapshot.Capture(new[]{command.After});
                var record=journal.Read(key)??journal.Prepare(key,operation,client.Creation,ordinal,before,after);
                if(record.State!=RebirthCamperTransferJournal.Phase.Prepared||!record.Before.SequenceEqual(before)||!record.After.SequenceEqual(after))return true;
                journal.Advance(key,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
                player.Buffs.SetCustomVar(InventorySourceReceipt(operation),-1f-ordinal,true);
                var value=command.After.Clone();var permit=new InventoryPermit{Client=client,Grid=command.Bag?player.bag.ItemGrid:player.inventory.ItemGrid,Slot=command.Slot,Value=value};
                if(inventoryPermit!=null)return true;inventoryPermit=permit;
                ObserveInventoryCall(operation,0);
                try{if(command.Bag)player.bag.SetSlot(command.Slot,value);else player.inventory.SetItem(command.Slot,value);}finally{inventoryPermit=null;}
                if(!permit.Consumed||!client.Current()||!client.Plan.MatchesPrefix(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,ordinal+1))return true;
                player.Buffs.SetCustomVar(InventorySourceReceipt(operation),100f+ordinal+1,true);PublishInventoryOwner(client);
                journal.Advance(key,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);return true;
            }
            if(phase==8)
            {
                float marker=player.Buffs.GetCustomVar(InventorySourceReceipt(operation));
                if(marker!=1f&&marker!=100f+commands.Length||!client.Plan.MatchesAfter(player.bag.ItemGrid.items,player.inventory.ItemGrid.items))return true;
                player.Buffs.SetCustomVar(InventorySourceReceipt(operation),1f,true);PublishInventoryOwner(client);return true;
            }
            if(player.Buffs.GetCustomVar(InventorySourceReceipt(operation))!=1f)return true;
            if(phase==4)
            {
                float marker=player.Buffs.GetCustomVar(InventorySecondaryReceipt(operation));
                if(marker==0f)
                {
                    player.Buffs.SetCustomVar(InventorySecondaryReceipt(operation),2f,true);
                    ObserveInventoryCall(operation,3);
                    player.Stats.Water.Value=UnityEngine.Mathf.Max(0f,player.Stats.Water.Value-client.HydrationCost);
                    if(!client.Current())return true;player.Buffs.SetCustomVar(InventorySecondaryReceipt(operation),1f,true);
                }
                if(player.Buffs.GetCustomVar(InventorySecondaryReceipt(operation))==1f)PublishInventoryOwner(client);return true;
            }
            if(phase==1&&player.Buffs.GetCustomVar(InventorySecondaryReceipt(operation))==1f)
            {
                float marker=player.Buffs.GetCustomVar(InventoryUseReceipt(operation));
                if(marker==0f)
                {
                    player.Buffs.SetCustomVar(InventoryUseReceipt(operation),2f,true);
                    ObserveInventoryCall(operation,4);
                    if(!RebirthPreservedConsumableEffects.TryApply(player,client.Serving,false,RebirthMetabolismService.DebugEnabled)||!client.Current())return true;
                    player.Buffs.SetCustomVar(InventoryUseReceipt(operation),1f,true);
                }
                if(player.Buffs.GetCustomVar(InventoryUseReceipt(operation))==1f)PublishInventoryOwner(client);return true;
            }
        }
        catch(Exception error){Log.Warning("[REBIRTH Consumption] original native owner retained: "+error.GetType().Name);}
        finally{preparing?.Memory.Dispose();}
        return true;
    }
}
