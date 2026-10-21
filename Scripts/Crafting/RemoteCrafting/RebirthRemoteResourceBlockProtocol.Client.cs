using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;
using HarmonyLib;

internal static partial class RebirthRemoteResourceBlockProtocol
{
    private sealed class PendingBlock
    {
        internal RebirthRemoteResourceClientOriginal.Intent Original;internal ItemActionRepair Action;internal ItemActionRepair.InventoryDataRepair Data;
        internal readonly string Nonce=Guid.NewGuid().ToString("N");internal ulong Exchange;internal float Next;internal bool Ack,Armed=true,Cancel,Rejecting,UsedTerminal;
        internal Guid Source;internal string SourceWorld,Binding,Birth,NoUse,ReplyHash;internal ItemStack[] Paid;internal XElement Refund;
        internal bool Awaiting,OutboundReference;internal int OutboundStage=-1;internal string OutboundPayload,Outbound;
    }
    private static readonly Dictionary<Guid,PendingBlock> clientPending=new Dictionary<Guid,PendingBlock>();
    private static World clientWorld;private static string clientDirectory;private static float clientScan;private static bool installed;
    private static RebirthRemoteResourceClientOriginalJournal ClientAcks()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteBlockAdmissionAcks"));
    internal static bool IsActive=>installed;
    internal static bool BlocksNative(ItemActionRepair action,ItemActionData data)
        =>installed&&data?.invData?.holdingEntity is EntityPlayerLocal player&&clientPending.Values.Any(value=>!value.UsedTerminal&&ClientCurrent(player,value)&&
            ReferenceEquals(value.Action,action)&&ReferenceEquals(value.Data,data));
    internal static void Install()
    {
        if(installed)return;
        if(GameManager.Instance?.World!=null)throw new InvalidOperationException("Block original protocol must install before World creation.");
        RebirthRemoteResourceBlockNativeCapture.Install();
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.remote.resources.original.block.input.v1"),typeof(RebirthRemoteResourceBlockPendingInput));
        ModEvents.GameUpdate.RegisterHandler(Update);installed=true;
    }
    private static void BindClient(World world)
    {
        string directory=GameIO.GetSaveGameDir();if(ReferenceEquals(world,clientWorld)&&directory==clientDirectory)return;
        clientPending.Clear();clientWorld=world;clientDirectory=directory;clientScan=0;
    }
    private static bool ClientCurrent(EntityPlayerLocal player,PendingBlock value)=>RebirthRemoteResourceClientOriginal.Current(player,value.Original)&&
        clientPending.TryGetValue(value.Original.Operation,out var retained)&&ReferenceEquals(value,retained);
    private static bool ClientTerminalScope(EntityPlayerLocal player,PendingBlock value)=>RebirthRemoteResourceBlockOriginal.TerminalScope(player,value.Original)&&
        clientPending.TryGetValue(value.Original.Operation,out var retained)&&ReferenceEquals(value,retained);
    private static bool SourceScope(EntityPlayerLocal player,PendingBlock value)=>ClientCurrent(player,value)&&value.Ack&&
        RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(value.Original,out _,out _,out _,out _);
    private static XElement AckBody(RebirthRemoteResourceClientOriginal.Intent original,string world,Guid source,string birth)=>new XElement("blockAdmissionAck",new XAttribute("original",original.Digest),
        new XAttribute("world",world),new XAttribute("source",source.ToString("N")),new XAttribute("birth",birth));
    private static bool Ack(EntityPlayerLocal player,PendingBlock value,string world,Guid source,string birth)
    {
        if(!ClientTerminalScope(player,value)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||
            source!=RebirthRemoteResourceDurableJournal.Operation(world,identity.StorageKey,value.Original.Creation,value.Original.Epoch,value.Original.Request,value.Original.Kind)||
            !Guid.TryParseExact(birth,"N",out var target)||target==Guid.Empty||target.ToString("N")!=birth)return false;
        var original=value.Original;byte[] bytes=Encoding.UTF8.GetBytes(AckBody(original,world,source,birth).ToString(SaveOptions.DisableFormatting));var journal=ClientAcks();
        var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
        if(record.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
        value.Ack=true;value.SourceWorld=world;value.Source=source;value.Birth=birth;return true;
    }
    private static bool ReadAck(EntityPlayerLocal player,PendingBlock value)
    {
        var record=ClientAcks().Read(value.Original.Operation);if(record==null)return false;
        if(record.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=value.Original.Operation||record.OwnerCreation!=value.Original.Creation||record.Before.Length>4096||!record.Before.SequenceEqual(record.After))throw new InvalidDataException();
        var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),4096);
        if(!Guid.TryParseExact((string)body.Attribute("source"),"N",out var source)||!XNode.DeepEquals(body,AckBody(value.Original,(string)body.Attribute("world"),source,(string)body.Attribute("birth"))))throw new InvalidDataException();
        return Ack(player,value,(string)body.Attribute("world"),source,(string)body.Attribute("birth"));
    }
    internal static bool Begin(EntityPlayerLocal player,ItemActionRepair action,ItemActionRepair.InventoryDataRepair data,Vector3i target,RemoteResourceClientOperation kind,ulong epoch,ulong request,List<ItemStack> full,List<ItemStack> remote)
    {
        try
        {
            BindClient(player?.world);
            if(!installed||!RebirthRemoteResourceRepairProtocol.SupportsClientTransport()||clientPending.Values.Count(value=>!value.UsedTerminal)>=32||
                !RebirthRemoteResourceBlockPlan.Capture(player,action,data,target,kind,epoch,request,full,remote,out var original)||!RebirthRemoteResourceOwnerAction.TryHoldPreparedOriginal(player,original))return false;
            clientPending.Add(original.Operation,new PendingBlock{Original=original,Action=action,Data=data});SaveNative(player,original,10);return true;
        }
        catch{return false;}
    }
    private static void SaveNative(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,int phase)
    {player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation),phase,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);}
    private static void AttachCurrent(EntityPlayerLocal player,PendingBlock value)
    {
        var body=value.Original.Copy().Element("action");var inv=player.inventory?.holdingItemData;int index=(int)body.Attribute("actionIndex");
        if(inv?.item?.Actions==null||inv.actionData==null||index<0||index>=inv.item.Actions.Length||index>=inv.actionData.Count||inv.slotIdx!=(int)body.Attribute("toolSlot")||
            inv.item.Actions[index] is not ItemActionRepair action||inv.actionData[index] is not ItemActionRepair.InventoryDataRepair data)return;
        value.Action=action;value.Data=data;
    }
    private static void Send(EntityPlayerLocal player,PendingBlock value,int stage,string payload="")
    {
        var user=GameManager.Instance.GetPersistentLocalPlayer()?.PrimaryId;if(user==null||value.Exchange==ulong.MaxValue)return;
        var original=value.Original;
        if(stage!=0&&stage!=9)GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
        if(!value.Awaiting||value.OutboundStage!=stage||value.OutboundPayload!=payload||value.OutboundReference!=value.Ack)
        {
            value.Exchange++;value.ReplyHash=null;
            XElement source=value.Ack?new XElement("originalRef",new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("digest",original.Digest),new XAttribute("epoch",original.Epoch),new XAttribute("request",original.Request)):original.Copy();
            var body=new XElement("blockRequest",new XAttribute("version",1),new XAttribute("stage",stage),new XAttribute("exchange",value.Exchange),new XAttribute("nonce",value.Nonce),source,new XElement("ownerActionBody",payload));
            value.Outbound=body.ToString(SaveOptions.DisableFormatting);value.OutboundStage=stage;value.OutboundPayload=payload;value.OutboundReference=value.Ack;value.Awaiting=true;
        }
        // Same immutable body/header while awaiting a fragmented reply. A
        // queued large frame must not become obsolete on every retry timer.
        NetPackageRebirthRemoteRepairOriginal.Send(player.entityId,user,original,value.Outbound);value.Next=Time.realtimeSinceStartup+1.5f;
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        var world=GameManager.Instance?.World;
        RebirthRemoteResourceBlockOptimisticView.Tick(world);
        if(world?.IsRemote()==false){ServerUpdate(world);return;}
        BindClient(world);if(world?.IsRemote()!=true)return;var player=world.GetPrimaryPlayer();if(player==null||player.IsDead()||!player.IsSpawned())return;
        float now=Time.realtimeSinceStartup;
        if(now>=clientScan)
        {
            clientScan=now+5;
            if(RebirthRemoteResourceClientOriginal.TryReadPending(out var originals))foreach(var original in originals)
            {
                if(original.Kind!=RemoteResourceClientOperation.BlockRepair&&original.Kind!=RemoteResourceClientOperation.BlockUpgrade||clientPending.Values.Count(value=>!value.UsedTerminal)>=32||clientPending.ContainsKey(original.Operation)||!RebirthRemoteResourceBlockOriginal.TerminalScope(player,original))continue;
                try
                {
                    var value=new PendingBlock{Original=original};clientPending.Add(original.Operation,value);ReadAck(player,value);
                    value.Cancel=RebirthRemoteResourceBlockOriginal.RejectedOrUnknown(original)||RebirthRemoteResourcePreAdmissionRefusal.ClientRequested(original);
                    value.Armed=false; // A cold Prepared original needs a real current rearm gesture, never a replay of an earlier release.
                    if(!ClientCurrent(player,value))continue;
                    AttachCurrent(player,value);
                    if(RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out var source,out var binding,out var paid))
                    {
                        value.Source=source;value.Binding=binding;value.Paid=paid;
                        if(RebirthRemoteResourceBlockRefund.HasClientOwed(original,source,binding,out _,out var noUse)){value.NoUse=noUse;continue;}
                        bool Scope()=>SourceScope(player,value);
                        if(RebirthRemoteResourceBlockOriginal.TryRead(original,out _,out _))
                        {if(!RebirthRemoteResourceOwnerAction.TryHoldBlockOriginal(player,original,Scope))RebirthRemoteResourceOwnerAction.TryHoldMixedBlock(player,original,Scope);}
                        else RebirthRemoteResourceOwnerAction.TryHoldPreparedOriginal(player,original);
                    }
                    else if(RebirthRemoteResourceOwnerAction.TryHoldPreparedOriginal(player,original)&&RebirthRemoteResourceBlockOriginal.CanUploadInitial(original)&&
                        (player.Buffs.GetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation))==0f||player.Buffs.GetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation))==10f))SaveNative(player,original,10);
                }
                catch{/* Retain original; unreadable local evidence is never a fresh admission. */}
            }
        }
        foreach(var value in clientPending.Values.ToArray())
        {
            if(!ClientTerminalScope(player,value)||now<value.Next)continue;
            if(!ClientCurrent(player,value)&&value.NoUse==null){Send(player,value,9);continue;}
            if(value.UsedTerminal){Send(player,value,9);value.Next=now+5;continue;}
            var original=value.Original;AttachCurrent(player,value);
            if(LocalPlayerUI.GetUIForPlayer(player)?.windowManager?.IsModalWindowOpen()==true)CancelPending(player,value);
            if(RebirthRemoteResourcePreAdmissionRefusal.ClientRequested(original)&&value.Paid==null)
            {
                if(RebirthRemoteResourcePreAdmissionRefusal.HasClientTerminal(original)&&RebirthRemoteResourceOwnerAction.ReleaseNeverAdmittedTerminal(player,original))
                {clientPending.Remove(original.Operation);continue;}
                RebirthRemoteResourcePreAdmissionRefusal.SendRequested(player,original);Send(player,value,10);continue;
            }
            if(value.NoUse!=null)
            {
                bool OwedScope()=>ClientTerminalScope(player,value)&&RebirthRemoteResourceBlockRefund.HasClientOwed(original,value.Source,value.Binding,out _,out _);
                if(value.Refund==null&&RebirthRemoteResourceBlockRefund.TryReadClientRequest(original,out var previousRefund))value.Refund=previousRefund;
                if(value.Refund!=null&&RebirthRemoteResourceBlockRefund.IsClientApplied(player,original,value.Refund))Send(player,value,7,value.Refund.ToString(SaveOptions.DisableFormatting));
                else if(RebirthRemoteResourceBlockRefund.PrepareClient(player,original,value.Source,value.Binding,OwedScope,out var refund))
                {value.Refund=refund;Send(player,value,RebirthRemoteResourceBlockRefund.IsClientApplied(player,original,refund)?7:6,refund.ToString(SaveOptions.DisableFormatting));}
                else value.Next=now+1.5f;
                continue;
            }
            string rejected=RebirthRemoteResourceBlockOriginal.TryReadPreparedRejection(original,out var rejection)?rejection.ToString(SaveOptions.DisableFormatting):string.Empty;
            if(value.Rejecting){Send(player,value,4,rejected);continue;}
            if(value.Cancel){Send(player,value,3,rejected);continue;}
            if(RebirthRemoteResourceBlockOriginal.TryReadCapturedCommand(original,out var changes,out var clear))
            {Send(player,value,2,new XElement("blockOwnerCommand",new XAttribute("clear",clear),new XElement("changes",changes)).ToString(SaveOptions.DisableFormatting));continue;}
            if(RebirthRemoteResourceBlockOriginal.TryRead(original,out _,out var state))
            {
                if(state==RebirthCamperTransferJournal.Phase.Prepared)Send(player,value,1);
                else Send(player,value,0); // Uncertain without a command is unknown, never callback replay.
                continue;
            }
            Send(player,value,0);
        }
    }
    private static void CancelPending(EntityPlayerLocal player,PendingBlock value)
    {
        value.Armed=false;
        if(value.Cancel||value.NoUse!=null||value.UsedTerminal)return;
        if(!ClientCurrent(player,value)||!RebirthRemoteResourceBlockOriginal.CanRejectUnentered(value.Original))return;
        value.Cancel=true;
        if(RebirthRemoteResourceBlockOriginal.TryRead(value.Original,out _,out _))RebirthRemoteResourceBlockOriginal.SavePreparedRejection(player,value.Original);
        else RebirthRemoteResourcePreAdmissionRefusal.RequestClient(player,value.Original);
        value.Next=0;
    }
    internal static bool BeforeHoldingUse(EntityAlive entity,int index,bool released,ref bool result)
    {
        try{return BeforeHoldingUseCore(entity,index,released,ref result);}
        catch{if(RebirthRemoteResourceOwnerAction.IsHeld(entity as EntityPlayerLocal)){result=false;return false;}return true;}
    }
    private static bool BeforeHoldingUseCore(EntityAlive entity,int index,bool released,ref bool result)
    {
        var player=entity as EntityPlayerLocal;if(player==null||!installed)return true;
        foreach(var value in clientPending.Values)
        {
            if(!ClientCurrent(player,value)||value.Action==null||value.Data==null||value.Data.indexInEntityOfAction!=index||
                !ReferenceEquals(player.inventory?.holdingItemData,value.Data.invData)||player.inventory.holdingItemIdx!=value.Data.invData.slotIdx||
                value.Data.invData.item?.Actions==null||index<0||index>=value.Data.invData.item.Actions.Length||!ReferenceEquals(value.Data.invData.item.Actions[index],value.Action)||
                value.Data.invData.actionData==null||index>=value.Data.invData.actionData.Count||!ReferenceEquals(value.Data.invData.actionData[index],value.Data))continue;
            var body=value.Original.Copy().Element("action");string tool=RebirthNativeItemCodec.Encode(value.Data.invData.itemValue);
            if(index!=(int)body.Attribute("actionIndex")||tool!=body.Element("toolInput").Value&&tool!=body.Element("toolAfter").Value)continue;
            if(value.UsedTerminal)return true;
            result=false;
            if(released)
            {
                value.Armed=false;
                RebirthRemoteResourceOwnerAction.ReleaseOriginalBlockInput(player,value.Action,value.Data,value.Original,index);
                return false; // Cost-free original release only, never callback replay.
            }
            if(value.NoUse!=null)
            {
                if(RebirthRemoteResourceOwnerAction.IsHeld(player))return false;
                value.Action=null;value.Data=null;return true; // A new actual gesture is a separate native original.
            }
            if(value.Cancel||value.Rejecting||RebirthRemoteResourceBlockOriginal.RejectedOrUnknown(value.Original))return false;
            if(!player.IsAttackValid()||index==0&&player.emodel?.avatarController?.IsAnimationAttackPlaying()==true||
                LocalPlayerUI.GetUIForPlayer(player)?.windowManager?.IsModalWindowOpen()!=false)return false;
            if(!value.Armed){value.Armed=true;value.Next=0;}
            return false; // Actual native input rearms only; saved permission still required.
        }
        return true;
    }
    internal static void Result(EntityPlayerLocal player,Guid operation,string text)
    {
        try
        {
            if(!clientPending.TryGetValue(operation,out var value)||!ClientTerminalScope(player,value))return;var original=value.Original;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,12*1024*1024);
            var names=new[]{"version","original","digest","nonce","exchange","admitted","sourceWorld","source","binding","birth","phase","outcome","permission","rejecting","ownerProof","noUse","disposition","command","decline","saved"};
            if(body.Name!="blockResult"||body.Attributes().Count()!=names.Length||body.Elements().Count()!=3||body.Nodes().Any(node=>node is not XElement)||
                names.Any(name=>body.Attribute(name)==null)||body.Element("paid")==null||body.Element("declinedBody")==null||body.Element("projection")==null||
                (int?)body.Attribute("version")!=1||(string)body.Attribute("original")!=operation.ToString("N")||(string)body.Attribute("digest")!=original.Digest||
                (string)body.Attribute("nonce")!=value.Nonce||(ulong)body.Attribute("exchange")!=value.Exchange)return;
            var canonical=new XElement("blockResult",names.Select(name=>new XAttribute(name,(string)body.Attribute(name))),new XElement("paid",body.Element("paid").Value),
                new XElement("declinedBody",body.Element("declinedBody").Value),new XElement("projection",body.Element("projection").Value));
            if(canonical.ToString(SaveOptions.DisableFormatting)!=text||(int)body.Attribute("admitted")!=1||!Guid.TryParseExact((string)body.Attribute("source"),"N",out var source)||
                !Ack(player,value,(string)body.Attribute("sourceWorld"),source,(string)body.Attribute("birth")))return;
            string replyHash=RebirthRemoteResourceDurableJournal.Digest(text);value.ReplyHash=replyHash;value.Awaiting=false;ulong exchange=value.Exchange;
            bool Auth()=>ClientTerminalScope(player,value)&&value.Exchange==exchange&&value.ReplyHash==replyHash;
            bool OriginalAuth()=>Auth()&&ClientCurrent(player,value);
            int phase=(int)body.Attribute("phase"),outcome=(int)body.Attribute("outcome"),permission=(int)body.Attribute("permission"),rejecting=(int)body.Attribute("rejecting");
            if(phase<0||phase>4||outcome<0||outcome>2||permission<0||permission>1||rejecting<0||rejecting>1)return;
            string binding=(string)body.Attribute("binding"),proof=(string)body.Attribute("ownerProof"),command=(string)body.Attribute("command"),decline=(string)body.Attribute("decline"),projection=body.Element("projection").Value;
            if(phase==4&&outcome==1&&(string)body.Attribute("disposition")=="used")
            {
                if(!RebirthRemoteResourceBlockOriginal.SaveUsedTerminal(player,original,source,binding,command,(string)body.Attribute("saved"),proof,Auth))return;
                bool projected=projection.Length>0&&RebirthRemoteResourceBlockProjection.ApplyUsed(player,original,source,binding,proof,projection,Auth);
                if(!RebirthRemoteResourceOwnerAction.ReleaseBlockUsedTerminal(player,original,source,binding,proof))return;
                value.UsedTerminal=true;
                if(projected)clientPending.Remove(operation);else value.Next=Time.realtimeSinceStartup+1.5f;
                while(clientPending.Values.Count(entry=>entry.UsedTerminal)>16)
                {
                    var old=clientPending.Values.First(entry=>entry.UsedTerminal);RebirthRemoteResourceBlockOptimisticView.Retire(old.Original);clientPending.Remove(old.Original.Operation);
                    // Full positive financial terminal/command remain durable.
                    // Removing a transient view retry grants no stale rewrite.
                }
                return; // View retry is independent of inventory/payment terminal bookkeeping.
            }
            string noUse=(string)body.Attribute("noUse");
            if(phase==3&&outcome==2&&noUse.Length==0){Send(player,value,ClientCurrent(player,value)?5:9);return;}
            if(phase==4&&outcome==2&&(string)body.Attribute("disposition")=="unused-no-payment")
            {if(RebirthRemoteResourceClientOriginal.SaveNoPaymentTerminal(player,original,(string)body.Attribute("sourceWorld"),source,binding,proof,Auth))clientPending.Remove(operation);return;}
            if(phase==4&&(outcome==1||outcome==2)&&(string)body.Attribute("disposition")=="refunded")
            {if(RebirthRemoteResourceBlockRefund.CompleteClient(player,original,source,binding,proof,Auth))clientPending.Remove(operation);return;}
            if(!ClientCurrent(player,value)&&noUse.Length==0)return;
            if(!(phase==2&&outcome==1||phase==3&&outcome==2&&noUse.Length>0))return;
            var paid=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(body.Element("paid").Value));
            if(!RebirthRemoteResourceClientOriginal.SaveBlockSource(player,original,(string)body.Attribute("sourceWorld"),source,binding,paid,Auth))return;
            value.Source=source;value.Binding=binding;value.Paid=ItemStack.Clone(paid);
            // Persist the authenticated typed decline even with no loaded
            // projection (or after reconnect directly into no-use outcome).
            // Optional view correction cannot be a prerequisite for owed
            // payment disposition. ApplyDeclined writes its exact mirror
            // before attempting the independently scoped view operation.
            if(decline.Length>0)RebirthRemoteResourceBlockProjection.ApplyDeclined(player,original,source,binding,command,body.Element("declinedBody").Value,decline,projection,Auth);
            if(noUse.Length>0)
            {
                if(!RebirthRemoteResourceBlockRefund.AcceptOwed(player,original,source,binding,noUse,command,decline,Auth))return;
                value.NoUse=noUse;
                if(value.Refund!=null&&permission==1&&RebirthRemoteResourceBlockRefund.ApplyClient(player,original,value.Refund,Auth))Send(player,value,7,value.Refund.ToString(SaveOptions.DisableFormatting));
                return;
            }
            if(decline.Length>0)
            {
                Send(player,value,8);return;
            }
            if(rejecting==1)
            {
                value.Rejecting=true;
                if(RebirthRemoteResourceOwnerAction.SaveRejectedBlockOwner(player,original,OriginalAuth))
                    Send(player,value,4,RebirthRemoteResourceBlockOriginal.TryReadPreparedRejection(original,out var rejection)?rejection.ToString(SaveOptions.DisableFormatting):string.Empty);
                return;
            }
            if(value.Cancel){Send(player,value,3,RebirthRemoteResourceBlockOriginal.TryReadPreparedRejection(original,out var rejection)?rejection.ToString(SaveOptions.DisableFormatting):string.Empty);return;}
            bool Source()=>SourceScope(player,value);
            if(!RebirthRemoteResourceBlockOriginal.TryRead(original,out _,out _)&&!RebirthRemoteResourceBlockOriginal.Prepare(player,original,value.Birth,Source,out _))return;
            if(!RebirthRemoteResourceOwnerAction.TryHoldBlockOriginal(player,original,Source))return;
            if(!RebirthRemoteResourceBlockOriginal.TryRead(original,out _,out var state)||state!=RebirthCamperTransferJournal.Phase.Prepared)return;
            float native=player.Buffs.GetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(operation));
            if(native==10f){SaveNative(player,original,1);Send(player,value,1);return;}
            if(native!=1f||permission!=1||!value.Armed||value.Action==null||value.Data==null)return;
            bool Permission()=>OriginalAuth()&&permission==1&&!value.Cancel&&!value.Rejecting&&value.Armed;
            if(RebirthRemoteResourceBlockExecution.TryExecute(player,value.Action,value.Data,original,Permission)&&RebirthRemoteResourceBlockOriginal.TryReadCapturedCommand(original,out var changes,out var clear))
                Send(player,value,2,new XElement("blockOwnerCommand",new XAttribute("clear",clear),new XElement("changes",changes)).ToString(SaveOptions.DisableFormatting));
        }
        catch{/* No legacy refund, callback replay or arbitrary owner image fallback. */}
    }
}
[HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.UseHoldingItem),new[]{typeof(int),typeof(bool)})]
internal static class RebirthRemoteResourceBlockPendingInput
{
    [HarmonyPriority(Priority.First+2)]
    private static bool Prefix(EntityAlive __instance,int _actionIndex,bool _isReleased,ref bool __result)
        =>RebirthRemoteResourceBlockProtocol.BeforeHoldingUse(__instance,_actionIndex,_isReleased,ref __result);
}
