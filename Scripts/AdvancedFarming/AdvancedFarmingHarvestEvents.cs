using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;

#nullable disable

// Items are NEVER replayed here. Interrupted opaque native callbacks retain an
// Invoking obligation unless the native owner file proves successful return.
// This is not a universal transaction for arbitrary XML/world side effects.
internal static class AdvancedFarmingHarvestEvents
{
    internal sealed class State
    {
        internal string Original,Signature;internal int Index,Phase;
        internal bool OwnerDone,StatisticsPending;
    }
    private static readonly Dictionary<Guid,State> Owners=new Dictionary<Guid,State>();
    private static readonly Dictionary<Guid,State> Servers=new Dictionary<Guid,State>();
    private static readonly HashSet<string> Returned=new HashSet<string>(StringComparer.Ordinal);
    private static World worldScope;private static string directoryScope;private static bool scanned;private static float next;
    private static string Key(Guid operation,int index)=>"rbFarmingEvent_"+operation.ToString("N")+"_"+index;
    private static AdvancedFarmingHarvestReceipt.Record Record(State s)=>AdvancedFarmingHarvestReceipt.Decode(s.Original);
    private static string Signature(AdvancedFarmingHarvestReceipt.Record record)
    {
        var r=AdvancedFarmingHarvestReceipt.Decode(AdvancedFarmingHarvestReceipt.Encode(record));r.State=AdvancedFarmingHarvestReceipt.Phase.Reserved;r.NativeSourceDigest=string.Empty;r.NativeSourceGeneration=0;r.DropOutputDigest=string.Empty;r.DropNativeDigest=string.Empty;r.DropNativeGeneration=0;
        return AdvancedFarmingHarvestReceipt.OutputDigest(AdvancedFarmingHarvestReceipt.Encode(r));
    }
    private static string[] Kinds(AdvancedFarmingHarvestReceipt.Record r)
    {
        var terminal=AdvancedFarmingHarvestTerminalPlot.Decode(r.Terminal);
        if(terminal!=null)return AdvancedFarmingHarvestReceipt.ReadOutputs(terminal.PlotOutputs).Length==0?new[]{"destroy","notify"}:new[]{"destroy","notify","quest","xp"};
        return RebirthUtilities.IsPlayerGrownHarvestCrop(new BlockValue{rawData=r.Raw})?Array.Empty<string>():new[]{"pickupQuest"};
    }
    private static string Encode(State s)=>new XElement("farmingEvents",new XAttribute("version",1),new XAttribute("signature",s.Signature),new XAttribute("index",s.Index),new XAttribute("phase",s.Phase),new XAttribute("ownerDone",s.OwnerDone),new XAttribute("statisticsPending",s.StatisticsPending),new XElement("original",s.Original)).ToString(SaveOptions.DisableFormatting);
    private static State Decode(string image)
    {
        if(image==null||image.Length>16*1024*1024)throw new InvalidDataException();XElement e;
        using(var text=new StringReader(image))using(var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16*1024*1024}))e=XElement.Load(reader);
        if(e.Name!="farmingEvents"||(int?)e.Attribute("version")!=1)throw new InvalidDataException();
        var s=new State{Original=(string)e.Element("original"),Signature=(string)e.Attribute("signature"),Index=(int)e.Attribute("index"),Phase=(int)e.Attribute("phase"),OwnerDone=(bool)e.Attribute("ownerDone"),StatisticsPending=(bool)e.Attribute("statisticsPending")};var r=Record(s);int count=Kinds(r).Length;
        if(!r.EventsRequired||s.Signature!=Signature(r)||s.Index<0||s.Index>count||s.Phase<0||s.Phase>2||s.OwnerDone&&s.Index!=count||Encode(s)!=image)throw new InvalidDataException();return s;
    }
    private static string PathFor(Guid operation,bool server)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","farming-harvest","v1","events",server?"server":"owner",operation.ToString("N")+".xml");
    private static bool Read(Guid operation,bool server,out State state)
    {
        state=null;try{string path=PathFor(operation,server);if(!File.Exists(path))path+=".done";if(new FileInfo(path).Length>64*1024*1024)return false;var s=Decode(File.ReadAllText(path,new UTF8Encoding(false,true)));if(Record(s).Operation!=operation)return false;state=s;return true;}catch{return false;}
    }
    private static bool Save(State s,bool server,string expected=null)
    {
        var r=Record(s);string path=PathFor(r.Operation,server),temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            string image=Encode(s);Decode(image);
            if(File.Exists(path)||File.Exists(path+".bak")||File.Exists(path+".done")){if(!Read(r.Operation,server,out var prior))return false;if(expected==null)return Encode(prior)==image;if(Encode(prior)!=expected||prior.Signature!=s.Signature||s.Index<prior.Index||prior.OwnerDone&&!s.OwnerDone)return false;}else if(expected!=null)return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path));var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
            return Encode(Decode(File.ReadAllText(temporary,new UTF8Encoding(false,true))))==image&&RebirthDurableFileCommit.TryPublish(temporary,path,out _);
        }
        catch{return false;}
    }
    private static void Scope(World world)
    {string directory=GameIO.GetSaveGameDir();if(ReferenceEquals(worldScope,world)&&directoryScope==directory)return;worldScope=world;directoryScope=directory;Owners.Clear();Servers.Clear();Returned.Clear();scanned=false;next=0f;}
    private static ItemStack StatisticsOutput(AdvancedFarmingHarvestReceipt.Record r)
    {var c=AdvancedFarmingHarvestTerminalPlot.Decode(r.Terminal);if(c==null||AdvancedFarmingHarvestReceipt.Remaining(r).Length!=0)return null;var outputs=AdvancedFarmingHarvestReceipt.ReadOutputs(c.PlotOutputs);return outputs.Length==1?outputs[0]:null;}
    internal static bool PrepareStatistics(EntityPlayer owner,AdvancedFarmingHarvestReceipt.Record r)
    {var output=StatisticsOutput(r);return output==null||RebirthStatisticsService.PrepareResourcesGatheredOriginal(owner,output.itemValue.ItemClass.GetItemName(),output.count,r.Operation.ToString("N"));}
    internal static bool StatisticsArchived(EntityPlayer owner,string operation,string item,int count)
    {
        try
        {
            if(owner?.world==null||owner.world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,owner.world)||!Guid.TryParseExact(operation,"N",out var id))return false;
            string path=PathFor(id,true)+".done";if(!File.Exists(path)||new FileInfo(path).Length>64*1024*1024)return false;var state=Decode(File.ReadAllText(path,new UTF8Encoding(false,true)));if(Record(state).Operation!=id||!state.OwnerDone||state.StatisticsPending)return false;
            var r=Record(state);if(r.World!=owner.world.worldState?.Guid||!RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity)||r.Owner!=identity.CanonicalId||!AdvancedFarmingHarvestReceipt.TryRead(r.World,id,out var main)||main.State!=AdvancedFarmingHarvestReceipt.Phase.Retired||Signature(main)!=state.Signature)return false;
            var output=StatisticsOutput(r);return output!=null&&output.count==count&&output.itemValue.ItemClass.GetItemName()==item;
        }
        catch{return false;}
    }
    internal static bool Required(AdvancedFarmingHarvestReceipt.Record r)=>Kinds(r).Length!=0;
    internal static bool Ensure(AdvancedFarmingHarvestReceipt.Record r)
    {
        if(!Required(r))return true;if(!r.EventsRequired)return false;
        if(Read(r.Operation,true,out var stored)){if(stored.Signature!=Signature(r))return false;Servers[r.Operation]=stored;return true;}
        var terminal=AdvancedFarmingHarvestTerminalPlot.Decode(r.Terminal);var s=new State{Original=AdvancedFarmingHarvestReceipt.Encode(r),Signature=Signature(r),StatisticsPending=terminal!=null&&AdvancedFarmingHarvestReceipt.ReadOutputs(terminal.PlotOutputs).Length!=0&&AdvancedFarmingHarvestReceipt.Remaining(r).Length==0};
        if(!Save(s,true))return false;Servers[r.Operation]=s;return true;
    }
    private static bool Current(World world,EntityPlayer owner,AdvancedFarmingHarvestReceipt.Record r)=>ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(owner?.world,world)&&ReferenceEquals(world.GetEntity(owner.entityId),owner)&&!owner.IsDead()&&owner.Died==r.Deaths&&world.worldState?.Guid==r.World&&RebirthCamperCharacterAccess.CurrentCreation(owner)==r.Creation&&RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity)&&identity.CanonicalId==r.Owner;
    private static EntityPlayer Owner(World world,AdvancedFarmingHarvestReceipt.Record r)
    {foreach(var player in world.Players.list)if(Current(world,player,r))return player;return null;}
    private static bool Archive(State s,bool server)
    {
        try{var r=Record(s);string path=PathFor(r.Operation,server),done=path+".done";if(File.Exists(done))return File.ReadAllText(done,new UTF8Encoding(false,true))==Encode(s);if(!File.Exists(path)||File.ReadAllText(path,new UTF8Encoding(false,true))!=Encode(s))return false;File.Move(path,done);return true;}catch{return false;}
    }
    private static void Reply(World world,EntityPlayer owner,State s)
    {
        var r=Record(s);if(!Current(world,owner,r)||!AdvancedFarmingHarvestReceipt.TryRead(r.World,r.Operation,out var main)||main.State!=AdvancedFarmingHarvestReceipt.Phase.Retired||Signature(main)!=s.Signature)return;
        AdvancedFarmingHarvestProtocol.ReplyEvents(owner,main,s.Index,s.OwnerDone);
    }
    internal static void Request(World world,EntityPlayer player,Guid operation)
    {if(world?.IsRemote()!=false||!Read(operation,true,out var s)||!Current(world,player,Record(s)))return;Reply(world,player,s);}
    internal static void Receive(World world,AdvancedFarmingHarvestReceipt.Record r,int index,bool done)
    {
        try
        {
            if(world==null||r.State!=AdvancedFarmingHarvestReceipt.Phase.Retired||!r.EventsRequired||!Current(world,world.GetPrimaryPlayer(),r)||index<0||index>Kinds(r).Length||done&&index!=Kinds(r).Length)return;Scope(world);
            if(!Read(r.Operation,false,out var s))
            {
                s=new State{Original=AdvancedFarmingHarvestReceipt.Encode(r),Signature=Signature(r),Index=index,OwnerDone=done};if(!Save(s,false))return;
            }
            else
            {
                if(s.Signature!=Signature(r)||index<s.Index||s.OwnerDone&&!done)return;
                if(index>s.Index||done&&!s.OwnerDone){string prior=Encode(s);s.Index=index;s.Phase=0;s.OwnerDone=done;if(!Save(s,false,prior)){Owners[r.Operation]=Decode(prior);return;}}
            }
            if(s.OwnerDone){Archive(s,false);Owners.Remove(r.Operation);}else Owners[r.Operation]=s;
        }
        catch(Exception failure){Log.Warning("[REBIRTH Farming] original native event reply retained: "+failure.GetType().Name);}
    }
    private static bool Ready(EntityPlayerLocal p)=>!AdvancedFarmingHarvestOwnerDelta.HasOwnHold(p)&&!RebirthCookingOutputDelivery.IsHeld(p)&&!RebirthLogisticsOwnerReceipt.IsHeld(p)&&!RebirthGearOwnerReservation.IsHeld(p)&&!RebirthBackpackLibraryReservation.IsHeld(p)&&!RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(p)&&!RebirthCamperStationNativeActorPayment.Blocks(p.bag)&&!RebirthCamperStationNativeActorPayment.Blocks(p.inventory);
    private static void Upload(){GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);}
    private static void Invoke(EntityPlayerLocal owner,AdvancedFarmingHarvestReceipt.Record r,string kind)
    {
        var source=new BlockValue{rawData=r.Raw};var position=new Vector3i(r.X,r.Y,r.Z);
        if(kind=="pickupQuest"){QuestEventManager.Current.BlockPickedUp(source.Block.GetBlockName(),position);return;}
        var c=AdvancedFarmingHarvestTerminalPlot.Decode(r.Terminal);var tool=AdvancedFarmingHarvestReceipt.ReadOutputs(c.Tool)[0].itemValue;var plot=new BlockValue{rawData=c.Raw};
        if(kind=="notify"){owner.NotifyDestroyedBlock(new ItemActionAttack.AttackHitInfo{hitRef=new BlockValueRef(position+Vector3i.down),raycastHitPosition=position+Vector3i.down,blockBeingDamaged=plot,bBlockHit=true,bKilled=true,bHarvestTool=c.HarvestTool,damageGiven=plot.Block.MaxDamage-plot.damage,damageMax=plot.Block.MaxDamage});return;}
        if(kind=="destroy")
        {
            var priorItem=owner.MinEventContext.ItemValue;var priorBlock=owner.MinEventContext.BlockValue;var priorTags=owner.MinEventContext.Tags;
            try{owner.MinEventContext.ItemValue=tool;owner.MinEventContext.BlockValue=plot;owner.MinEventContext.Tags=plot.Block.Tags;owner.FireEvent(MinEventTypes.onSelfDestroyedBlock,c.UseInventory??true);}
            finally{if(ReferenceEquals(owner.MinEventContext.ItemValue,tool))owner.MinEventContext.ItemValue=priorItem;if(owner.MinEventContext.BlockValue.rawData==plot.rawData)owner.MinEventContext.BlockValue=priorBlock;if(owner.MinEventContext.Tags.Equals(plot.Block.Tags))owner.MinEventContext.Tags=priorTags;}
            return;
        }
        var output=AdvancedFarmingHarvestReceipt.ReadOutputs(c.PlotOutputs)[0];
        if(kind=="quest"){QuestEventManager.Current.HarvestedItem(tool,output.Clone(),plot);return;}
        if(kind=="xp"){owner.Progression.AddLevelExp((int)(output.itemValue.ItemClass.MadeOfMaterial.Experience*output.count),"_xpFromHarvesting",Progression.XPTypes.Harvesting);return;}
        throw new InvalidDataException();
    }
    private static void PumpOwner(World world,EntityPlayerLocal owner,State s)
    {
        var r=Record(s);if(!Current(world,owner,r)||s.OwnerDone||s.Index>=Kinds(r).Length||!Ready(owner)||!RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity))return;
        var plan=AdvancedFarmingHarvestOwnerDelta.Decode(r.PlanImage);string key=Key(r.Operation,s.Index),returned=r.Operation.ToString("N")+":"+s.Index;
        if(s.Phase==0)
        {
            if(!AdvancedFarmingHarvestOwnerDelta.SavedPhase(identity,plan.Receipt,out float phase)||phase!=3f)return;
            string prior=Encode(s);s.Phase=1;if(!Save(s,false,prior)){s.Phase=0;return;}
            try{Invoke(owner,r,Kinds(r)[s.Index]);if(!Current(world,owner,r))return;owner.Buffs.SetCustomVar(key,2f);Returned.Add(returned);}
            catch(Exception failure){Log.Warning("[REBIRTH Farming] interrupted original native event retained without replay: "+r.Operation.ToString("N")+"/"+s.Index+" "+failure.GetType().Name);return;}
        }
        if(s.Phase==1)
        {
            if(!Returned.Contains(returned)&&!AdvancedFarmingHarvestOwnerDelta.SavedPair(identity,plan.Receipt,3f,key,2f))return;
            string prior=Encode(s);s.Phase=2;if(!Save(s,false,prior)){s.Phase=1;Upload();return;}Returned.Remove(returned);
        }
        if(s.Phase==2)Upload();
    }
    internal static void Pump(World world)
    {
        if(world==null)return;Scope(world);if(UnityEngine.Time.realtimeSinceStartup<next)return;next=UnityEngine.Time.realtimeSinceStartup+1f;
        string role=world.IsRemote()?"owner":"server";
        if(!scanned)
        {
            scanned=true;string directory=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","farming-harvest","v1","events",role);
            try{if(Directory.Exists(directory))foreach(string path in Directory.EnumerateFiles(directory,"*.xml").Take(100001)){try{if(new FileInfo(path).Length>64*1024*1024)continue;var s=Decode(File.ReadAllText(path,new UTF8Encoding(false,true)));if(!world.IsRemote()||!s.OwnerDone)(world.IsRemote()?Owners:Servers)[Record(s).Operation]=s;}catch{/* Retain forensic primary. */}}}catch{scanned=false;}
        }
        if(!world.IsRemote())foreach(var pair in Servers.ToArray())
        {
            try
            {
                var s=pair.Value;var r=Record(s);var owner=Owner(world,r);if(owner==null||!AdvancedFarmingHarvestReceipt.TryRead(r.World,r.Operation,out var main)||main.State!=AdvancedFarmingHarvestReceipt.Phase.Retired||Signature(main)!=s.Signature||!RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity))continue;
                var plan=AdvancedFarmingHarvestOwnerDelta.Decode(r.PlanImage);if(!AdvancedFarmingHarvestOwnerDelta.SavedPhase(identity,plan.Receipt,out float phase)||phase!=3f)continue;
                if(s.Index<Kinds(r).Length&&AdvancedFarmingHarvestOwnerDelta.SavedPair(identity,plan.Receipt,3f,Key(r.Operation,s.Index),2f))
                {string prior=Encode(s);s.Index++;s.OwnerDone=s.Index==Kinds(r).Length;if(!Save(s,true,prior)){Servers[pair.Key]=Decode(prior);continue;}}
                if(s.OwnerDone&&s.StatisticsPending)
                {
                    var output=StatisticsOutput(r);if(output==null||!RebirthStatisticsService.RecordResourcesGatheredOriginal(owner,output.itemValue.ItemClass.GetItemName(),output.count,r.Operation.ToString("N")))continue;
                    string prior=Encode(s);s.StatisticsPending=false;if(!Save(s,true,prior)){Servers[pair.Key]=Decode(prior);continue;}
                }
                Reply(world,owner,s);if(s.OwnerDone&&Archive(s,true)){RebirthStatisticsService.RetireResourcesGatheredOriginal(owner,r.Operation.ToString("N"));Servers.Remove(pair.Key);}
            }
            catch(Exception failure){Log.Warning("[REBIRTH Farming] native event obligation retained: "+failure.GetType().Name);}
        }
        var local=world.GetPrimaryPlayer();if(local==null)return;
        foreach(var s in Owners.Values.ToArray())
        {
            try{PumpOwner(world,local,s);var r=Record(s);if(Current(world,local,r))AdvancedFarmingHarvestProtocol.RequestEvents(local,r.Operation);}
            catch(Exception failure){Log.Warning("[REBIRTH Farming] owner native event obligation retained: "+failure.GetType().Name);}
        }
    }
}
