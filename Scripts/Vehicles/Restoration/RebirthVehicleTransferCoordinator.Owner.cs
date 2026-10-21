using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthVehicleTransferCoordinator
{
    private static readonly Dictionary<Guid,RebirthVehicleTransferPlan> ownerPlans=new Dictionary<Guid,RebirthVehicleTransferPlan>();
    private static Guid ReadyReceipt(RebirthVehicleTransferPlan plan)
    {using(var sha=System.Security.Cryptography.SHA256.Create()){var hash=sha.ComputeHash(Encoding.UTF8.GetBytes("vehicle-owner-ready-v1|"+plan.ToXml().ToString(SaveOptions.DisableFormatting)));var bytes=new byte[16];Array.Copy(hash,bytes,16);return new Guid(bytes);}}
    private static Guid StartedReceipt(Guid operation)
    {using(var sha=System.Security.Cryptography.SHA256.Create()){var hash=sha.ComputeHash(Encoding.UTF8.GetBytes("vehicle-owner-started-v1|"+operation.ToString("N")));var bytes=new byte[16];Array.Copy(hash,bytes,16);return new Guid(bytes);}}
    internal static bool HasSavedNeverEnteredOwner(EntityPlayer player,Guid operation,Func<bool> current)
    {
        if(current==null||!current()||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||!RebirthGearNativePlayerFile.TryRead(identity,out var native)||native.deaths!=player.Died||
            native.buffData==null||native.buffData.Length<1||native.buffData.Length>1024*1024||!RebirthVehicleReceiptReader.TryRead(native.buffData.ToArray(),new[]{StartedReceipt(operation)},out var stages))return false;
        return stages[StartedReceipt(operation)]==RebirthVehicleOwnerTransferResult.Pending&&current();
    }
    private static partial bool EnsureOwnerReady(Transfer transfer)
    {
        if(!transfer.Current())return false;
        if(transfer.Player is EntityPlayerLocal local)
        {bool localReady=ApplyOwner(local,transfer.Plan,new RebirthVehicleTransferItem[0],transfer.Current);transfer.OwnerRejected=!localReady&&ReadyStage(local,transfer.Plan)==RebirthVehicleOwnerTransferResult.Rejected;return localReady;}
        if(RebirthStablePlayerIdentity.TryResolveServerEntity(transfer.Player,out var identity)&&RebirthGearNativePlayerFile.TryRead(identity,out var native)&&
            native.deaths==transfer.Player.Died&&native.buffData!=null&&native.buffData.Length>0&&native.buffData.Length<=1024*1024&&
            RebirthVehicleReceiptReader.TryRead(native.buffData.ToArray(),new[]{ReadyReceipt(transfer.Plan)},out var ready)&&
            transfer.Current())
        {
            transfer.OwnerRejected=ready[ReadyReceipt(transfer.Plan)]==RebirthVehicleOwnerTransferResult.Rejected;
            if(transfer.OwnerRejected)return false;
            if(ready[ReadyReceipt(transfer.Plan)]==RebirthVehicleOwnerTransferResult.Applied)return true;
        }
        SendOwner(transfer,new RebirthVehicleTransferItem[0],false);return false;
    }
    private static RebirthVehicleOwnerTransferResult ReadyStage(EntityPlayer player,RebirthVehicleTransferPlan plan)
    {
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||!RebirthGearNativePlayerFile.TryRead(identity,out var native)||native.deaths!=player.Died||
            native.buffData==null||native.buffData.Length<1||native.buffData.Length>1024*1024||!RebirthVehicleReceiptReader.TryRead(native.buffData.ToArray(),new[]{ReadyReceipt(plan)},out var stages))return RebirthVehicleOwnerTransferResult.Indeterminate;
        return stages[ReadyReceipt(plan)];
    }
    private static bool PreflightOwner(EntityPlayerLocal player,RebirthVehicleTransferPlan plan)
    {
        if(!RebirthVehicleClientCustody.HasCurrentTransferSourcePolicy(player,plan)||RebirthVehicleTransferCustody.Image(player.bag.ItemGrid.items,player.inventory.ItemGrid.items)!=plan.Custody.OwnerImageHash)return false;
        var bag=RebirthVehicleInventoryWrite.Snapshot(player.bag.ItemGrid.items);var belt=RebirthVehicleInventoryWrite.Snapshot(player.inventory.ItemGrid.items);
        foreach(var item in plan.Items.OrderBy(item=>!item.Debit))
        {
            if(!item.Debit)
            {
                if(RebirthVehicleOwnerCreditPlan.Prepare(bag,belt,RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length),item,out var creditBag,out var creditBelt,
                    index=>RebirthVehicleNativePolicy.IsLocked(player,true,index),index=>RebirthVehicleNativePolicy.IsLocked(player,false,index))!=RebirthVehicleInventoryPlanResult.Ready)return false;
                bag=creditBag;belt=creditBelt;continue;
            }
            bool inBag=!item.Debit||item.Source==RebirthVehiclePartSourceLocation.Backpack;
            var result=RebirthVehicleOwnerInventoryPlan.Prepare(inBag?bag:belt,item,inBag?bag.Length:RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length),
                index=>RebirthVehicleNativePolicy.IsLocked(player,inBag,index),out var candidate);
            if(result!=RebirthVehicleInventoryPlanResult.Ready)return false;
            if(inBag)bag=candidate;else belt=candidate;
        }
        return true;
    }
    private static partial bool OfferOwner(Transfer transfer,RebirthVehicleTransferItem[] operations)
    {
        if(transfer==null||!transfer.Current()||operations==null||operations.Length==0)return false;
        if(transfer.Player is EntityPlayerLocal local)return ApplyOwner(local,transfer.Plan,operations,transfer.Current);
        return SendOwner(transfer,operations,false);
    }
    private static partial bool CompleteOwner(Transfer transfer)
    {
        if(!transfer.Current())return false;if(transfer.Player is EntityPlayerLocal)return true;
        if(transfer.OwnerAcknowledged&&HasSavedOwnerSeen(transfer))return true;
        SendOwner(transfer,new RebirthVehicleTransferItem[0],true);return false;
    }
    private static bool HasSavedOwnerSeen(Transfer transfer)
    {
        if(!transfer.Current())return false;
        if(RebirthStablePlayerIdentity.TryResolveServerEntity(transfer.Player,out var identity)&&RebirthGearNativePlayerFile.TryRead(identity,out var native)&&
            native.deaths==transfer.Player.Died&&native.buffData!=null&&native.buffData.Length>0&&native.buffData.Length<=1024*1024&&
            RebirthVehicleReceiptReader.TryRead(native.buffData.ToArray(),new[]{transfer.Plan.TransactionId},out var seen)&&
            seen[transfer.Plan.TransactionId]==RebirthVehicleOwnerTransferResult.Applied&&transfer.Current())return true;
        return false;
    }
    internal static bool ReceiveOwnerAcknowledgment(World world,ClientInfo sender,Guid operation,string hash)
        =>ThreadManager.IsMainThread()&&world!=null&&!world.IsRemote()&&ReferenceEquals(world,GameManager.Instance?.World)&&sender!=null&&
            TryGet(operation,out var transfer)&&ReferenceEquals(transfer.Player.world,world)&&transfer.Acknowledge(sender,hash);
    private static bool ApplyOwner(EntityPlayerLocal player,RebirthVehicleTransferPlan plan,RebirthVehicleTransferItem[] operations,Func<bool> authenticatedOriginal)
    {
        if(!RebirthVehicleClientCustody.MatchesTransferPlan(player,plan)||!authenticatedOriginal())return false;
        bool Current()=>authenticatedOriginal()&&RebirthVehicleClientCustody.HasCurrentTransferSourcePolicy(player,plan)&&RebirthVehicleOwnerCustody.Matches(player,plan);
        if(operations.Length==0)
        {
            bool Restore()=>authenticatedOriginal()&&RebirthVehicleClientCustody.MatchesTransferPlan(player,plan);
            var started=StartedReceipt(plan.TransactionId);player.Buffs.SetCustomVar("rbVehicle_"+started.ToString("N"),1f,true);
            if(!RebirthVehicleOwnerCheckpoint.Persist(player,new[]{started},1f,Restore))return false;
            var stage=ReadyStage(player,plan);var ready=ReadyReceipt(plan);
            if(stage==RebirthVehicleOwnerTransferResult.Rejected)return false;
            if(stage!=RebirthVehicleOwnerTransferResult.Applied&&!PreflightOwner(player,plan))
            {
                if(!Restore())return false;player.Buffs.SetCustomVar("rbVehicle_"+ready.ToString("N"),-1f,true);
                RebirthVehicleOwnerCheckpoint.Persist(player,new[]{ready},-1f,Restore);
                if(Restore()&&player.world.IsRemote())GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return false;
            }
            if(!RebirthVehicleOwnerCustody.TryAcquire(player,plan,authenticatedOriginal))return false;
            if(stage!=RebirthVehicleOwnerTransferResult.Applied&&RebirthVehicleTransferCustody.Image(player.bag.ItemGrid.items,player.inventory.ItemGrid.items)!=plan.Custody.OwnerImageHash)return false;
            player.Buffs.SetCustomVar("rbVehicle_"+ready.ToString("N"),1f,true);
            if(!RebirthVehicleOwnerCheckpoint.Persist(player,new[]{ready},1f,Current))return false;
            if(Current()&&player.world.IsRemote())GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
            return Current();
        }
        if(ReadyStage(player,plan)!=RebirthVehicleOwnerTransferResult.Applied)return false;
        if(!RebirthVehicleOwnerCustody.TryAcquire(player,plan,authenticatedOriginal))return false;
        RebirthVehicleOwnerTransferResult result;
        if(operations.Length>1)
        {
            if(operations.Any(item=>!item.Debit)||!operations.Select(item=>item.ReceiptId).SequenceEqual(plan.Items.Where(item=>item.Debit).Select(item=>item.ReceiptId)))return false;
            result=RebirthVehicleOwnerDebitBatch.Apply(player,plan.CreationId,plan.CreationId,operations,Current);
        }
        else result=operations[0].Debit?RebirthVehicleOwnerTransfer.Apply(player,plan.CreationId,plan.CreationId,operations[0],Current):RebirthVehicleOwnerCreditApply.Apply(player,plan,operations[0],Current);
        if(Current()&&player.world.IsRemote())GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
        return result==RebirthVehicleOwnerTransferResult.Applied||result==RebirthVehicleOwnerTransferResult.Rejected;
    }
    private static bool SendOwner(Transfer transfer,RebirthVehicleTransferItem[] operations,bool terminal)
    {
        try
        {
            if(!transfer.Current())return false;
            var frame=new XElement("vehicleOwnerOffer",new XAttribute("terminal",terminal),new XAttribute("rejected",transfer.Rejected),new XAttribute("noEffect",transfer.NeverEntered),transfer.Plan.ToXml(),
                new XElement("operations",operations.Select(item=>new XElement("id",item.ReceiptId))));
            var body=new RebirthRemoteResourceFragments.Body(Encoding.UTF8.GetBytes(frame.ToString(SaveOptions.DisableFormatting)));
            if(body.Length>3*1024*1024)return false;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;NetPackageManager.GetPackageId(typeof(NetPackageRebirthVehicleOwnerFragment));
            for(int index=0;index<body.Count;index++)
            {
                if(!transfer.Current()||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance))return false;
                manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthVehicleOwnerFragment>().Setup(transfer.Player.entityId,transfer.Plan.TransactionId,body,index),_attachedToEntityId:transfer.Player.entityId);
            }
            return transfer.Current();
        }
        catch{return false;}
    }
    internal static bool ReceiveOwnerFragment(World world,int playerId,Guid operation,string digest,int total,int count,int index,string part)
    {
        try
        {
            var player=world?.GetPrimaryPlayer();var game=GameManager.Instance;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(!IsAdmitting||!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(game?.World,world)||player==null||player.entityId!=playerId||
                !ReferenceEquals(world.GetEntity(playerId),player)||player.IsDead()||!player.IsSpawned()||manager==null||manager.IsServer||
                manager.connectionToServer==null||manager.connectionToServer.Length==0||manager.connectionToServer[0]==null||manager.connectionToServer[0].IsDisconnected()||
                total<1||total>3*1024*1024)return false;
            var peer=manager.connectionToServer[0];int deaths=player.Died;string directory=GameIO.GetPlayerDataDir();
            bool Scope()=>IsAdmitting&&ThreadManager.IsMainThread()&&ReferenceEquals(world,GameManager.Instance?.World)&&ReferenceEquals(game,GameManager.Instance)&&
                ReferenceEquals(player.world,world)&&ReferenceEquals(world.GetPrimaryPlayer(),player)&&ReferenceEquals(world.GetEntity(playerId),player)&&player.Died==deaths&&!player.IsDead()&&player.IsSpawned()&&
                ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager.connectionToServer!=null&&manager.connectionToServer.Length>0&&
                ReferenceEquals(peer,manager.connectionToServer[0])&&!peer.IsDisconnected()&&directory==GameIO.GetPlayerDataDir();
            string scope="vehicle-owner|"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(peer)+"|"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(player)+"|"+world.Guid;
            if(!RebirthRemoteResourceFragments.TryAdd(scope,operation,digest,total,count,index,part,Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency,out var bytes))return false;
            if(bytes==null)return true;
            XElement frame;using(var stream=new MemoryStream(bytes))using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=3*1024*1024}))frame=XElement.Load(reader);
            if(frame.Name!="vehicleOwnerOffer"||frame.Attributes().Count()!=3||frame.Elements().Count()!=2||!Scope())return false;
            var plan=RebirthVehicleTransferPlan.Read(frame.Element("vehicleTransferPlan"));
            bool terminal=(bool)frame.Attribute("terminal"),rejected=(bool)frame.Attribute("rejected");
            bool priorLife=terminal&&plan.NativeDeaths>=0&&plan.NativeDeaths<player.Died;
            if(plan.TransactionId!=operation||!(priorLife?RebirthVehicleClientCustody.CanRetireCompletedTransfer(player,plan,rejected):
                terminal?RebirthVehicleClientCustody.MatchesTerminalDescription(player,plan):RebirthVehicleClientCustody.MatchesTransferPlan(player,plan)))return false;
            if(ownerPlans.TryGetValue(operation,out var old)&&old.ToXml().ToString()!=plan.ToXml().ToString()||!ownerPlans.ContainsKey(operation)&&ownerPlans.Count>=64)return false;
            ownerPlans[operation]=plan;
            if(terminal)return ReceiveOwnerTerminal(player,plan,rejected,(bool)frame.Attribute("noEffect"),Scope);
            var ids=frame.Element("operations").Elements("id").Select(element=>Guid.Parse(element.Value)).ToArray();
            if(ids.Length>256||ids.Distinct().Count()!=ids.Length)return false;
            var items=ids.Select(id=>plan.Items.Single(item=>item.ReceiptId==id)).ToArray();
            return ApplyOwner(player,plan,items,Scope);
        }
        catch{return false;}
    }
    private static bool ReceiveOwnerTerminal(EntityPlayerLocal player,RebirthVehicleTransferPlan plan,bool rejected,bool noEffect,Func<bool> authenticated)
    {
        if(plan.NativeDeaths>=0&&plan.NativeDeaths<player.Died)
        {
            bool Description()=>authenticated()&&RebirthVehicleClientCustody.CanRetireCompletedTransfer(player,plan,rejected);
            if(!Description())return false;
            // Only the authenticated original source's positive saved terminal
            // reaches here. No old item receipt, item setter or world grant is
            // reconstructed in this new life. Cursor obligations stay escrowed.
            player.Buffs.SetCustomVar("rbVehicle_"+plan.TransactionId.ToString("N"),1f,true);
            if(!RebirthVehicleOwnerCheckpoint.Persist(player,new[]{plan.TransactionId},1f,Description)||
                !RebirthVehicleOwnerCustody.RetireHistorical(player,plan,Description))return false;
            return SendOwnerTerminalAcknowledgment(player,plan,Description);
        }
        bool Saved()
        {
            if(!authenticated()||!RebirthVehicleClientCustody.MatchesTerminalDescription(player,plan)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||
                !RebirthGearNativePlayerFile.TryRead(identity,out var native)||native.deaths!=player.Died||native.buffData==null||native.buffData.Length<1||native.buffData.Length>1024*1024||
                !RebirthVehicleReceiptReader.TryRead(native.buffData.ToArray(),plan.Items.Select(item=>item.ReceiptId),out var receipts))return false;
            return (!noEffect||rejected&&ReadyStage(player,plan)==RebirthVehicleOwnerTransferResult.Rejected)&&
                plan.Items.All(item=>receipts[item.ReceiptId]==(noEffect?RebirthVehicleOwnerTransferResult.Pending:rejected?(item.Debit?RebirthVehicleOwnerTransferResult.Rejected:RebirthVehicleOwnerTransferResult.Pending):RebirthVehicleOwnerTransferResult.Applied))&&authenticated();
        }
        if(!Saved())return false;
        player.Buffs.SetCustomVar("rbVehicle_"+plan.TransactionId.ToString("N"),1f,true);
        if(!RebirthVehicleOwnerCheckpoint.Persist(player,new[]{plan.TransactionId},1f,()=>authenticated()&&Saved()))return false;
        if(RebirthVehicleOwnerCustody.Matches(player,plan)&&!RebirthVehicleOwnerCustody.Release(player,plan,Saved))return false;
        if(!RebirthVehicleOwnerCustody.RetireHistorical(player,plan,()=>authenticated()&&Saved()&&player.Buffs.GetCustomVar("rbVehicle_"+plan.TransactionId.ToString("N"))==1f))return false;
        return SendOwnerTerminalAcknowledgment(player,plan,authenticated);
    }
    private static bool SendOwnerTerminalAcknowledgment(EntityPlayerLocal player,RebirthVehicleTransferPlan plan,Func<bool> authenticated)
    {
        GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
        if(!authenticated())return false;
        NetPackageManager.GetPackageId(typeof(NetPackageRebirthVehicleOwnerAcknowledgment));
        var packet=NetPackageManager.GetPackage<NetPackageRebirthVehicleOwnerAcknowledgment>().Setup(plan.TransactionId,RebirthVehicleTransferCustody.Hash(plan.ToXml().ToString(SaveOptions.DisableFormatting)));
        if(!authenticated())return false;SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(packet);ownerPlans.Remove(plan.TransactionId);return authenticated();
    }
}
