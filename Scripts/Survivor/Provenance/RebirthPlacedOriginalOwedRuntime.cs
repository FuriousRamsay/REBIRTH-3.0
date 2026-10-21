using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    private sealed class OwedAttempt{internal OwedChild Child;internal EntityPlayerLocal Player;internal World World;internal string Directory;internal float Next;}
    private sealed class OwedPermit{internal OwedAttempt Attempt;internal ItemStackGrid Grid;internal int Slot;internal ItemStack Value;internal bool Used;}
    private static readonly Dictionary<Guid,OwedAttempt> owedAttempts=new Dictionary<Guid,OwedAttempt>();
    private static readonly Dictionary<Guid,int> owedCompleted=new Dictionary<Guid,int>();
    private static World owedWorld;private static string owedDirectory;private static OwedPermit owedPermit;
    private static void OwedScope(World world){if(ReferenceEquals(owedWorld,world)&&owedDirectory==GameIO.GetSaveGameDir())return;owedAttempts.Clear();owedCompleted.Clear();owedPermit=null;owedWorld=world;owedDirectory=GameIO.GetSaveGameDir();}
    private static bool OwedAttemptCurrent(OwedAttempt a)=>a!=null&&OwedCurrent(a.Player,a.Child)&&ReferenceEquals(a.World,GameManager.Instance?.World)&&a.Directory==GameIO.GetSaveGameDir()&&a.Player.IsSpawned();
    internal static bool IsOwedHeld(EntityPlayerLocal player)=>owedAttempts.Values.Any(a=>ReferenceEquals(a.Player,player)&&a.Child.Phase<3&&OwedAttemptCurrent(a));
    internal static bool BlocksOwed(ItemStackGrid grid)=>owedAttempts.Values.Any(a=>a.Child.Phase<3&&OwedAttemptCurrent(a)&&(ReferenceEquals(grid,a.Player.bag?.ItemGrid)||ReferenceEquals(grid,a.Player.inventory?.ItemGrid)));
    internal static bool ConsumeOwed(ItemStackGrid grid,int index,ItemStack value)
    {
        var p=owedPermit;if(p==null||p.Used||!OwedAttemptCurrent(p.Attempt)||p.Attempt.Child.Phase!=2||p.Attempt.Child.Applied||!ReferenceEquals(p.Grid,grid)||p.Slot!=index||!ReferenceEquals(p.Value,value)||!RebirthCamperEnergyActorImage.Capture(p.Attempt.Player).Matches(RebirthPlacedOriginal.ReadActor(p.Attempt.Child.Before)))return false;p.Used=true;return true;
    }
    private static void UploadOwed(EntityPlayerLocal player)
    {GameManager.Instance.SaveLocalPlayerData();if(player.world.IsRemote())SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(player),false);}
    private static void SendOwed(EntityPlayerLocal player,string image)
    {if(player.world.IsRemote())SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPlacedOwedRequest>().Setup(player.entityId,image));else ReceiveOwedServer((World)player.world,player.entityId,image);}
    private static void ReplyOwed(World world,EntityPlayer actor,string image)
    {if(actor is EntityPlayerLocal local&&ReferenceEquals(world.GetPrimaryPlayer(),local))ReceiveOwedClient(world,local.entityId,image);else SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPlacedOwedReply>().Setup(actor.entityId,image),_attachedToEntityId:actor.entityId);}
    private static XElement OwedXml(string image)
    {if(image==null||image.Length>Maximum)throw new InvalidDataException();using(var input=new StringReader(image))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=Maximum}))return XElement.Load(reader);}
    private static bool OwedOwner(EntityPlayer actor,OwedPartition p)=>actor!=null&&actor.world?.worldState?.Guid==p.World&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&RebirthCamperCharacterAccess.CurrentCreation(actor)==p.Creation&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&identity.StorageKey==p.Owner;
    private static bool TrySavedOwedTerminal(EntityPlayer actor,OwedChild original,out OwedChild terminal)
    {
        terminal=null;try{return OwedIdentity(actor,original)&&actor.Died>=original.Deaths&&ReadChild(ChildPath(original,true),out terminal)&&SameChild(terminal,original)&&terminal.Phase>=3&&FullHash(terminal.Proof)&&ReadOwed(terminal.Parent,out var p)&&ValidOwedPartitionSource(p)&&ReadHead(terminal.Parent,out _)&&OwedHeadFor(p,out var h)&&h.Sequence>=terminal.Sequence;}catch{terminal=null;return false;}
    }
    private static bool ReplySavedOwedTerminal(EntityPlayer actor,OwedChild original)
    {if(!TrySavedOwedTerminal(actor,original,out var terminal))return false;ReplyOwed((World)actor.world,actor,ChildBody(terminal));return true;}
    private static bool RetireLocalOwedTerminal(OwedAttempt a)
    {try{if(a?.World?.IsRemote()!=false||!ReferenceEquals(a.World.GetPrimaryPlayer(),a.Player)||!TrySavedOwedTerminal(a.Player,a.Child,out var terminal))return false;ReceiveOwedClient(a.World,a.Player.entityId,ChildBody(terminal));return !owedAttempts.ContainsKey(a.Child.Parent);}catch{return false;}}
    private static void QueryOwed(World world,EntityPlayer actor,Guid parent,int afterSequence,int depth=0)
    {
        if(afterSequence<0||!ReadOwed(parent,out var p)||!OwedOwner(actor,p)||!ValidOwedPartitionSource(p)||!OwedHeadFor(p,out var head)||afterSequence>head.Sequence)return;
        if(afterSequence<head.Sequence){var key=new OwedChild{Parent=parent,Sequence=head.Sequence};if(ReadChild(ChildPath(key,true),out var terminal)&&terminal.Phase>=3&&AdvanceOwedHead(terminal))ReplyOwed(world,actor,ChildBody(terminal));return;}
        var pending=new OwedChild{Parent=parent,Sequence=head.Sequence+1};if(OwedRejectionExists(pending)){if(RecoverOwedSlotRejection(pending,out var rejected))ReplyOwed(world,actor,ChildBody(rejected));return;}string path=ChildPath(pending,true);if(!RecoverChildFile(pending,true))return;if(File.Exists(path)||File.Exists(path+".bak")){if(ReadChild(path,out var retained)){TrackOwedNativeChild(actor,retained);if(CompleteOwedChild(actor,retained,out var completed))retained=completed;if(retained.Phase<3&&ReadOwed(retained.Operation,out var nested)&&OwedOwner(actor,nested)&&ValidOwedPartitionSource(nested)&&OwedHeadFor(nested,out var nestedHead)){if(depth<64)QueryOwed(world,actor,nested.Operation,nestedHead.Sequence,depth+1);else ReplyOwed(world,actor,new XElement("placedOwedContinue",new XAttribute("parent",nested.Operation.ToString("N")),new XAttribute("after",nestedHead.Sequence),new XAttribute("world",nested.World),new XAttribute("creation",nested.Creation)).ToString(SaveOptions.DisableFormatting));return;}ReplyOwed(world,actor,ChildBody(retained));}return;}
        ReplyOwed(world,actor,new XElement("placedOwedOffer",new XElement("partition",p.Image),new XElement("head",HeadBody(head))).ToString(SaveOptions.DisableFormatting));
    }
    internal static void ObserveOwedSaved(EntityPlayer actor,Guid parent)
    {try{if(actor?.world?.IsRemote()!=false||!ReadOwed(parent,out var p)||!OwedOwner(actor,p)||!OwedHeadFor(p,out var head))return;var key=new OwedChild{Parent=parent,Sequence=head.Sequence+1};if(ReadChild(ChildPath(key,true),out var pending))CompleteOwedChild(actor,pending,out _);}catch{}}
    internal static void ReceiveOwedServer(World world,int entity,string image)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||!(world.GetEntity(entity) is EntityPlayer actor))return;var xml=OwedXml(image);
            if(xml.Name=="placedOwedUnused"){ReceiveOwedUnused(actor,image);return;}
            if(xml.Name=="placedOwedReject"){RejectUnpermittedOwed(actor,image);return;}
            if(xml.Name=="placedOwedPhysical"){ReceiveOwedPhysical(actor,image);return;}
            if(xml.Name=="placedOwedDelete"){ReceiveOwedDelete(actor,image);return;}
            if(xml.Name=="placedOwedQuery"){QueryOwed(world,actor,Guid.ParseExact((string)xml.Attribute("parent"),"N"),(int)xml.Attribute("after"));return;}
            var request=DecodeChild(image);if(request.Phase==0){if(PrepareOwedChild(actor,request,out var accepted))ReplyOwed(world,actor,ChildBody(accepted));}else if(request.Phase==2&&CompleteOwedChild(actor,request,out var terminal))ReplyOwed(world,actor,ChildBody(terminal));
        }catch{/* Exact owed original remains retained. */}
    }
    private static void ApplyOwed(OwedAttempt a)
    {
        if(!OwedAttemptCurrent(a))return;var c=a.Child;if(c.Phase==0){StampOwed(a.Player,c,1);UploadOwed(a.Player);SendOwed(a.Player,ChildBody(c));return;}
        if(c.Phase==1)
        {
            if(!RebirthCamperEnergyActorImage.Capture(a.Player).Matches(RebirthPlacedOriginal.ReadActor(c.Before)))return;string previous=ChildBody(c);var attempted=DecodeChild(previous);attempted.Phase=2;if(!SaveChild(attempted,false,previous))return;a.Child=c=attempted;
            var value=(c.Belt?RebirthPlacedOriginal.ReadActor(c.After).Belt:RebirthPlacedOriginal.ReadActor(c.After).Bag)[c.Slot].Clone();var permit=new OwedPermit{Attempt=a,Grid=c.Belt?a.Player.inventory.ItemGrid:a.Player.bag.ItemGrid,Slot=c.Slot,Value=value};var prior=owedPermit;try{owedPermit=permit;permit.Grid.SetItem(c.Slot,value);}finally{owedPermit=prior;}
            if(!permit.Used||!OwedAttemptCurrent(a)||!RebirthCamperEnergyActorImage.Capture(a.Player).Matches(RebirthPlacedOriginal.ReadActor(c.After)))return;previous=ChildBody(c);var observed=DecodeChild(previous);observed.Applied=true;if(!SaveChild(observed,false,previous))return;a.Child=c=observed;
        }
        if(c.Phase==2&&c.Applied&&RebirthCamperEnergyActorImage.Capture(a.Player).Matches(RebirthPlacedOriginal.ReadActor(c.After))){StampOwed(a.Player,c,2);UploadOwed(a.Player);SendOwed(a.Player,ChildBody(c));}
        // Phase2 without positive setter readback NEVER retries its native effect.
    }
    internal static void ReceiveOwedClient(World world,int entity,string image)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world)||!(world.GetPrimaryPlayer() is EntityPlayerLocal player)||player.entityId!=entity)return;OwedScope(world);var xml=OwedXml(image);
            if(xml.Name=="placedOwedContinue"){if((string)xml.Attribute("world")==world.worldState.Guid&&(string)xml.Attribute("creation")==RebirthCamperCharacterAccess.CurrentCreation(player)){Guid next=Guid.ParseExact((string)xml.Attribute("parent"),"N");int after=(int)xml.Attribute("after");if(next!=Guid.Empty&&after>=0)SendOwed(player,new XElement("placedOwedQuery",new XAttribute("parent",next.ToString("N")),new XAttribute("after",after)).ToString(SaveOptions.DisableFormatting));}return;}
            if(xml.Name=="placedOwedOffer")
            {
                var p=DecodeOwed((string)xml.Element("partition"));var head=DecodeHead((string)xml.Element("head"));if(head.Parent!=p.Operation||head.Partition!=Hash(p.Image)||!OwedOwner(player,p)||player.IsDead()||player.Died<p.Deaths||!RebirthPlacedOriginal.CanAdmitOwed(player))return;
                var rows=ReadRows(Convert.FromBase64String(p.Owed));if(rows.Length!=head.Delivered.Length)return;int row=-1;for(int i=0;i<rows.Length;i++){if(head.Delivered[i]>rows[i].count)return;if(row<0&&head.Delivered[i]<rows[i].count)row=i;}if(row<0)return;
                var key=new OwedChild{Parent=p.Operation,Sequence=head.Sequence+1};string path=ChildPath(key,false);if(!RecoverChildFile(key,false))return;if(File.Exists(path)||File.Exists(path+".bak")){if(!ReadChild(path,out var cold))return;if(!OwedCurrent(player,cold)){RequestOwedRejection(player,cold);return;}var restored=new OwedAttempt{Child=cold,Player=player,World=world,Directory=GameIO.GetSaveGameDir()};owedAttempts[p.Operation]=restored;ApplyOwed(restored);return;}
                var before=RebirthCamperEnergyActorImage.Capture(player);var owed=rows[row].Clone();owed.count-=head.Delivered[row];if(!TryPlanOwedFragment(before,owed,out var after,out bool belt,out int slot,out int quantity))return;
                var c=new OwedChild{Parent=p.Operation,Operation=Guid.NewGuid(),World=p.World,Owner=p.Owner,Creation=p.Creation,Deaths=player.Died,Partition=Hash(p.Image),Head=Hash(HeadBody(head)),Sequence=head.Sequence+1,Row=row,DeliveredBefore=head.Delivered[row],Quantity=quantity,Slot=slot,Belt=belt,Before=RebirthPlacedOriginal.EncodeActor(before),After=RebirthPlacedOriginal.EncodeActor(after)};if(!CanRepresentOwedRecovery(c,RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment))||!SaveChild(c,false))return;var attempt=new OwedAttempt{Child=c,Player=player,World=world,Directory=GameIO.GetSaveGameDir()};owedAttempts[p.Operation]=attempt;ApplyOwed(attempt);return;
            }
            var response=DecodeChild(image);if(!OwedIdentity(player,response)||!RecoverChildFile(response,false)||!ReadChild(ChildPath(response,false),out var local)||!SameChild(local,response))return;
            if(response.Phase>=3)
            {
                string old=ChildBody(local);if(!SaveChild(response,false,old))return;if(owedAttempts.TryGetValue(response.Parent,out var held)&&SameChild(held.Child,response))owedAttempts.Remove(response.Parent);owedCompleted.TryGetValue(response.Parent,out int previousCompleted);owedCompleted[response.Parent]=Math.Max(previousCompleted,response.Sequence);return;
            }
            if(response.Phase==1&&!OwedCurrent(player,local)){RequestOwedRejection(player,local);RetryOwedUnused(player,local);RetryOwedDelete(player,local);RetryOwedPhysical(player,local);return;}if(response.Phase!=1||!OwedCurrent(player,local))return;if(local.Phase==0){string old=ChildBody(local);local.Phase=1;if(!SaveChild(local,false,old))return;}
            var currentAttempt=new OwedAttempt{Child=local,Player=player,World=world,Directory=GameIO.GetSaveGameDir()};owedAttempts[local.Parent]=currentAttempt;ApplyOwed(currentAttempt);
        }catch{/* Unknown native setter outcome is retained, never replayed. */}
    }
    internal static object CaptureOwedAuditState(EntityPlayer actor,Guid parent)
    {
        var result=new Dictionary<string,object>{{"operation",parent.ToString("N")},{"available",false}};
        try
        {
            if(!ThreadManager.IsMainThread()||parent==Guid.Empty||actor?.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,actor.world)){result["status"]="unsupported-authority";return result;}
            string path=OwedPath(parent);if(!ReadOwed(parent,out var p)){result["status"]=File.Exists(path)?"corrupt-partition":"missing-partition";return result;}if(!OwedOwner(actor,p)){result["status"]="owner-scope-mismatch";return result;}
            result["available"]=true;result["status"]="positive-partition";result["partitionProof"]=p.Native;result["traceDigest"]=p.Trace;result["partitionDeaths"]=p.Deaths;result["currentDeaths"]=actor.Died;var rows=ReadRows(Convert.FromBase64String(p.Owed));result["owedRows"]=rows.Length;result["owedQuantity"]=rows.Sum(row=>(long)row.count);
            if(!ReadHead(parent,out var head)){result["headStatus"]=File.Exists(HeadPath(parent))?"corrupt":"not-admitted";return result;}if(head.Partition!=Hash(p.Image)||head.Delivered.Length!=rows.Length||head.Delivered.Where((value,index)=>value>rows[index].count).Any()){result["headStatus"]="corrupt-scope-or-count";return result;}result["headStatus"]="present";result["sequence"]=head.Sequence;result["deliveredQuantity"]=head.Delivered.Sum(value=>(long)value);result["headDigest"]=Hash(HeadBody(head));
            var key=new OwedChild{Parent=parent,Sequence=head.Sequence+1};result["childStatus"]=File.Exists(ChildPath(key,true))?"corrupt":"absent";if(ReadChild(ChildPath(key,true),out var child)){result["childStatus"]="present";result["child"]=child.Operation.ToString("N");result["childPhase"]=child.Phase;result["childDeaths"]=child.Deaths;result["childQuantity"]=child.Quantity;result["childRow"]=child.Row;result["childAppliedObservation"]=child.Applied;result["childProofPresent"]=FullHash(child.Proof);result["beforeDigest"]=Hash(child.Before);result["afterDigest"]=Hash(child.After);result["sameLife"]=child.Deaths==actor.Died;if(ReadOwedPhysical(child.Operation,true,out var physicalTrace)){result["physicalTraceDigest"]=physicalTrace.Digest;result["physicalOverflow"]=physicalTrace.Overflow;result["physicalFailedQuantity"]=physicalTrace.Inputs.Where(input=>physicalTrace.Overflow||input.Failed).Sum(input=>(long)input.Item.count);result["physicalNativeCheckpoint"]=ReadOwedNative(OwedNativePath(child.Operation),physicalTrace,out var nativeProof);result["physicalNativeProof"]=nativeProof??string.Empty;result["physicalBodyPublished"]=physicalTrace.Creation.Length!=0&&RebirthPlacedOriginalBodyPublication.TryPublished(child.Operation,physicalTrace.Creation,out _);}if(ReadOwed(child.Operation,out var nested)){result["successorPartition"]=Hash(nested.Image);result["successorQuantity"]=ReadRows(Convert.FromBase64String(nested.Owed)).Sum(row=>(long)row.count);if(ReadHead(nested.Operation,out var nestedHead)){result["successorSequence"]=nestedHead.Sequence;result["successorDelivered"]=nestedHead.Delivered.Sum(value=>(long)value);}}}
            return result;
        }catch{result["status"]="retained-read-failure";return result;}
    }
    internal static void UpdateOwed(World world)
    {
        try{if(!ThreadManager.IsMainThread()||world?.GetPrimaryPlayer() is not EntityPlayerLocal player)return;OwedScope(world);foreach(var attempt in owedAttempts.Values.ToArray()){if(!OwedIdentity(player,attempt.Child)||UnityEngine.Time.realtimeSinceStartup<attempt.Next)continue;attempt.Next=UnityEngine.Time.realtimeSinceStartup+1f;if(OwedAttemptCurrent(attempt))ApplyOwed(attempt);else {RequestOwedRejection(player,attempt.Child);RetryOwedUnused(player,attempt.Child);RetryOwedDelete(player,attempt.Child);RetryOwedPhysical(player,attempt.Child);}owedCompleted.TryGetValue(attempt.Child.Parent,out int completed);SendOwed(player,new XElement("placedOwedQuery",new XAttribute("parent",attempt.Child.Parent.ToString("N")),new XAttribute("after",completed)).ToString(SaveOptions.DisableFormatting));}}catch{}
    }
    private static void RetryOwed(EntityPlayerLocal player,Guid parent)
    {
        try
        {
            OwedScope((World)player.world);if(owedAttempts.TryGetValue(parent,out var attempt)&&OwedAttemptCurrent(attempt)){if(UnityEngine.Time.realtimeSinceStartup<attempt.Next)return;attempt.Next=UnityEngine.Time.realtimeSinceStartup+1f;ApplyOwed(attempt);}
            if(owedAttempts.TryGetValue(parent,out var retained)){RequestOwedRejection(player,retained.Child);RetryOwedUnused(player,retained.Child);RetryOwedDelete(player,retained.Child);RetryOwedPhysical(player,retained.Child);}owedCompleted.TryGetValue(parent,out int completed);SendOwed(player,new XElement("placedOwedQuery",new XAttribute("parent",parent.ToString("N")),new XAttribute("after",completed)).ToString(SaveOptions.DisableFormatting));
        }catch{}
    }
}
[UnityEngine.Scripting.Preserve]
public sealed class NetPackageRebirthPlacedOwedRequest:NetPackage
{
    private int player;private string image=string.Empty;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;public NetPackageRebirthPlacedOwedRequest Setup(int id,string text){player=id;image=text;return this;}
    public override void read(PooledBinaryReader reader){player=reader.ReadInt32();image=RebirthPlacedOriginalDeathPartition.ReadWire(reader);}public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write(player);RebirthPlacedOriginalDeathPartition.WriteWire(writer,image);}public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==false&&ValidEntityIdForSender(player))RebirthPlacedOriginalDeathPartition.ReceiveOwedServer(world,player,image);}public int GetLength()=>8+Encoding.UTF8.GetByteCount(image);
}
[UnityEngine.Scripting.Preserve]
public sealed class NetPackageRebirthPlacedOwedReply:NetPackage
{
    private int player;private string image=string.Empty;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;public NetPackageRebirthPlacedOwedReply Setup(int id,string text){player=id;image=text;return this;}
    public override void read(PooledBinaryReader reader){player=reader.ReadInt32();image=RebirthPlacedOriginalDeathPartition.ReadWire(reader);}public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write(player);RebirthPlacedOriginalDeathPartition.WriteWire(writer,image);}public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==true)RebirthPlacedOriginalDeathPartition.ReceiveOwedClient(world,player,image);}public int GetLength()=>8+Encoding.UTF8.GetByteCount(image);
}
