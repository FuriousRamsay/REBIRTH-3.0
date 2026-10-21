using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

// Personal queue only. Source payment, callback permission, native acceptance
// and terminal delivery are separate durable stages of one original action.
internal static class RebirthRemoteResourceCraftProtocol
{
    private sealed class Pending
    {
        internal RebirthRemoteResourceClientOriginal.Intent Original;
        internal ItemActionEntryCraft Entry;
        internal float Next;
        internal bool Ack,Rejecting,Used,RefundApplied;
        internal Guid Source;
        internal string Binding,SourceWorld,NoUse;
        internal ItemStack[] Paid;
        internal XElement Refund;
    }
    private static readonly Dictionary<Guid,Pending> pending=new Dictionary<Guid,Pending>();
    private sealed class LocalRequest
    {internal EntityPlayerLocal Player;internal World World;internal RebirthRemoteResourceClientOriginal.Intent Original;internal string Body;}
    private static readonly Dictionary<Guid,LocalRequest> localRequests=new Dictionary<Guid,LocalRequest>();
    private static World world;private static string directory;private static float scanAt;private static bool installed;
    private static RebirthRemoteResourceClientOriginalJournal Admissions()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteCraftSourceAdmission"));
    private static RebirthRemoteResourceClientOriginalJournal Acks()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteCraftSourceAcks"));
    internal static void Install()
    {if(installed)return;ModEvents.GameUpdate.RegisterHandler(Update);installed=true;}
    private static void Bind(World current)
    {string path=GameIO.GetSaveGameDir();if(ReferenceEquals(world,current)&&directory==path)return;pending.Clear();localRequests.Clear();world=current;directory=path;scanAt=0;}
    internal static bool Begin(EntityPlayerLocal player,ItemActionEntryCraft entry,ulong epoch,ulong request,List<ItemStack> full,List<ItemStack> remote)
    {
        try
        {
            Bind(player?.world);
            if(!installed||player?.world==null||player.world.IsRemote()&&!RebirthRemoteResourceRepairProtocol.SupportsClientTransport()||pending.Count>=32||
                !RebirthRemoteResourceCraftOriginal.Capture(player,entry,epoch,request,full,remote,out var original)||
                !RebirthRemoteResourceOwnerAction.TryHoldPreparedOriginal(player,original))return false;
            pending.Add(original.Operation,new Pending{Original=original,Entry=entry});SaveNative(player,original,10);return true;
        }
        catch{return false;}
    }
    internal static bool TryAttach(ItemActionEntryCraft entry)
    {
        var player=entry?.ItemController?.xui?.playerUI?.entityPlayer;var recipe=(entry?.ItemController as XUiC_RecipeEntry)?.Recipe;
        if(recipe==null||!RebirthRemoteResourceCraftOriginal.IsPersonalEntry(entry)||entry.craftCountControl?.WindowGroup?.Controller.GetChildByType<XUiC_WorkstationInputGrid>()!=null)return false;
        foreach(var value in pending.Values)
        {
            var action=value.Original.Copy().Element("action");
            if(RebirthRemoteResourcePreAdmissionRefusal.ClientRequested(value.Original)||!RebirthRemoteResourceClientOriginal.Current(player,value.Original)||entry.craftCountControl.Count!=(int)action.Attribute("count")||
                entry.craftingTier!=(int)action.Attribute("quality")||RebirthRemoteResourceOwnerAction.RecipeImage(recipe)!=action.Element("baseRecipe").Value)continue;
            value.Entry=entry;value.Next=0;return true;
        }
        return false;
    }
    internal static bool HasPendingForUi(XUi ui)=>ui!=null&&pending.Values.Any(value=>value.NoUse==null&&
        ReferenceEquals(value.Entry?.ItemController?.xui,ui)&&Current(ui.playerUI?.entityPlayer,value));
    internal static void CancelUnadmittedForUi(XUi ui)
    {
        var player=ui?.playerUI?.entityPlayer;if(player==null)return;
        foreach(var value in pending.Values.ToArray())
            if(!value.Ack&&RebirthRemoteResourcePreAdmissionRefusal.RequestClient(player,value.Original))
            {value.Entry=null;value.Next=0;RebirthRemoteResourcePreAdmissionRefusal.SendRequested(player,value.Original);}
    }
    private static void SaveNative(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,int phase)
    {player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation),phase,true);GameManager.Instance.SaveLocalPlayerData();if(player.world.IsRemote())GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);}
    private static bool Current(EntityPlayerLocal player,Pending value)=>RebirthRemoteResourceCraftAdoption.QueryScope(player,value.Original)&&
        pending.TryGetValue(value.Original.Operation,out var retained)&&ReferenceEquals(value,retained);
    private static bool Ack(RebirthRemoteResourceClientOriginal.Intent original,string sourceWorld,Guid operation)
    {
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(GameManager.Instance?.World?.GetPrimaryPlayer(),out var owner)||
            RebirthRemoteResourceDurableJournal.Operation(sourceWorld,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind)!=operation)return false;
        byte[] bytes=Encoding.UTF8.GetBytes(new XElement("craftAdmissionAck",new XAttribute("original",original.Digest),new XAttribute("world",sourceWorld),new XAttribute("operation",operation.ToString("N"))).ToString(SaveOptions.DisableFormatting));
        return Publish(Acks(),original.Operation,original,bytes);
    }
    private static bool HasAck(RebirthRemoteResourceClientOriginal.Intent original)
    {
        var record=Acks().Read(original.Operation);if(record==null)return false;
        if(record.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||!record.Before.SequenceEqual(record.After))throw new InvalidDataException();
        var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),4096);
        if(body.Name!="craftAdmissionAck"||body.Attributes().Count()!=3||body.Nodes().Any()||(string)body.Attribute("original")!=original.Digest||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(GameManager.Instance?.World?.GetPrimaryPlayer(),out var owner)||
            !Guid.TryParseExact((string)body.Attribute("operation"),"N",out var operation)||
            RebirthRemoteResourceDurableJournal.Operation((string)body.Attribute("world"),owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind)!=operation)throw new InvalidDataException();
        return true;
    }
    private static bool Publish(RebirthRemoteResourceClientOriginalJournal journal,Guid operation,RebirthRemoteResourceClientOriginal.Intent original,byte[] bytes)
    {
        var record=journal.Prepare(operation,original.Operation,original.Creation,0,bytes,bytes);
        if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    private static void Send(EntityPlayerLocal player,Pending value,int stage,string action="")
    {
        if(!Current(player,value))return;
        var persistent=GameManager.Instance.GetPersistentLocalPlayer();if(player.world.IsRemote()&&persistent?.PrimaryId==null)return;
        if(stage!=0&&player.world.IsRemote())GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
        var original=value.Original;
        XElement source=value.Ack?new XElement("originalRef",new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("digest",original.Digest),new XAttribute("epoch",original.Epoch),new XAttribute("request",original.Request)):original.Copy();
        var request=new XElement("craftRequest",new XAttribute("version",2),new XAttribute("stage",stage),source,new XElement("ownerActionBody",action));
        if(!player.world.IsRemote())
        {
            // Preserve the asynchronous stage boundary. A direct Request/Result
            // recursion could retry a refused native save indefinitely.
            localRequests[original.Operation]=new LocalRequest{Player=player,World=player.world,Original=original,Body=request.ToString(SaveOptions.DisableFormatting)};
            return;
        }
        NetPackageRebirthRemoteRepairOriginal.Send(player.entityId,persistent.PrimaryId,original,request.ToString(SaveOptions.DisableFormatting));
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        var current=GameManager.Instance?.World;Bind(current);if(current==null)return;
        var player=current.GetPrimaryPlayer();if(player==null||player.IsDead()||!player.IsSpawned())return;float now=Time.realtimeSinceStartup;
        if(now>=scanAt)
        {
            scanAt=now+5;
            if(RebirthRemoteResourceClientOriginal.TryReadPending(out var originals))foreach(var original in originals)
            {
                if(original.Kind!=RemoteResourceClientOperation.Craft||RebirthRemoteResourceFetchedStationOriginal.IsCandidate(original)||RebirthRemoteResourcePlacedStationOriginal.IsCandidate(original)||pending.Count>=32||pending.ContainsKey(original.Operation)||!RebirthRemoteResourceCraftAdoption.QueryScope(player,original))continue;
                try
                {
                    var value=new Pending{Original=original,Ack=HasAck(original)};pending.Add(original.Operation,value);
                    bool Auth()=>Current(player,value);
                    if(RebirthRemoteResourceRepairRefund.Bootstrap(player,original,Auth))continue;
                    if(player.Died>original.Deaths)continue;
                    if(RebirthRemoteResourceCraftOriginal.TryRead(original,out var action,out _))
                    {
                        if(!RebirthRemoteResourceOwnerAction.TryHoldRestoredCraft(player,original,Auth)&&
                            !RebirthRemoteResourceOwnerAction.TryHold(player,original,Guid.ParseExact((string)action.Attribute("source"),"N"),(string)action.Attribute("binding"),
                                RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor").Value),RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value),Auth))
                            RebirthRemoteResourceOwnerAction.TryHoldMixedCraft(player,original,Auth);
                    }
                    else if(RebirthRemoteResourceOwnerAction.TryHoldPreparedOriginal(player,original))SaveNative(player,original,10);
                }
                catch{/* Corrupt retained identity cannot be made into a fresh action. */}
            }
        }
        foreach(var value in pending.Values.ToArray())
        {
            if(!Current(player,value)||now<value.Next)continue;value.Next=now+1;
            try
            {
                if(RebirthRemoteResourcePreAdmissionRefusal.HasClientTerminal(value.Original))
                {if(RebirthRemoteResourceClientOriginal.CompleteNeverAdmitted(player,value.Original))pending.Remove(value.Original.Operation);continue;}
                if(RebirthRemoteResourcePreAdmissionRefusal.ClientRequested(value.Original))
                {value.Entry=null;RebirthRemoteResourcePreAdmissionRefusal.SendRequested(player,value.Original);}
                bool Auth()=>Current(player,value);
                if(value.NoUse!=null)
                {
                    if(RebirthRemoteResourceRepairRefund.TryPrepare(player,value.Original,value.Source,value.Binding,value.NoUse,value.Paid,Auth,out var refund))
                    {value.Refund=refund;value.RefundApplied|=RebirthRemoteResourceRepairRefund.IsApplied(player,value.Original,refund);Send(player,value,value.RefundApplied?7:6,refund.ToString(SaveOptions.DisableFormatting));}
                    else Send(player,value,0);
                }
                else if(RebirthRemoteResourceCraftOriginal.TryRead(value.Original,out _,out var phase))
                {
                    if(RebirthRemoteResourceCraftOriginal.ObserveLocalUsage(player,value.Original))
                    {RebirthRemoteResourceOwnerAction.TryHoldRestoredCraft(player,value.Original,Auth);SaveNative(player,value.Original,2);Send(player,value,2);}
                    else if(value.Rejecting){if(RebirthRemoteResourceCraftOriginal.SaveOwnerFive(player,value.Original))Send(player,value,4);else Send(player,value,3);}
                    else Send(player,value,phase==RebirthCamperTransferJournal.Phase.Prepared?1:8);
                }
                else Send(player,value,0);
            }
            catch{/* Retain the immutable original and exact native hold. */}
        }
        DrainLocalRequests(player,current);
    }
    private static void DrainLocalRequests(EntityPlayerLocal player,World current)
    {
        foreach(var pair in localRequests.ToArray())
        {
            localRequests.Remove(pair.Key);var request=pair.Value;
            if(ReferenceEquals(request.World,current)&&!current.IsRemote()&&ReferenceEquals(request.Player,player)&&
                pending.TryGetValue(pair.Key,out var retained)&&retained.Original.Digest==request.Original.Digest&&Current(player,retained))
                Request(player,request.Body);
        }
    }
    private static bool Scope(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)=>ThreadManager.IsMainThread()&&actor?.world?.IsRemote()==false&&!actor.IsDead()&&
        ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&original.Kind==RemoteResourceClientOperation.Craft&&
        actor.Died==original.Deaths&&actor.world.worldState.Guid==original.World&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.CanonicalId==original.Owner&&
        RebirthOriginalActionScope.Matches(actor,owner.StorageKey,original.Creation);
    private static bool Admit(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(!Scope(actor,original)&&!RebirthRemoteResourceCraftAdoption.HostScope(actor,original)||!RebirthRemoteResourceCraftOriginal.ValidateInput(original,out _,out _,out _,out int belt,out _)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string sourceWorld))return false;
        Guid operation=RebirthRemoteResourceDurableJournal.Operation(sourceWorld,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
        if(RebirthRemoteResourcePreAdmissionRefusal.BlocksAdmission(operation))return false;
        byte[] bytes=Encoding.UTF8.GetBytes(new XElement("craftSourceAdmission",new XAttribute("originalDigest",original.Digest),original.Copy()).ToString(SaveOptions.DisableFormatting));
        var journal=Admissions();var retained=journal.Read(operation);
        if(retained!=null)return retained.Vehicle==original.Operation&&retained.OwnerCreation==original.Creation&&retained.Before.SequenceEqual(bytes)&&retained.After.SequenceEqual(bytes)&&Publish(journal,operation,original,bytes);
        if(actor.Died!=original.Deaths)return false;
        if(!RebirthRemoteResourceCraftingRead.TryRead(owner,out var before,out var saved,out string queue)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
            !RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value).Matches(before)||queue!=original.Copy().Element("queueBefore").Value||belt!=actor.inventory.Length||
            !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=10f)return false;
        var needs=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("requirements").Value));
        var remote=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("remoteRequirements").Value));
        if(!RebirthRemoteResourceCraftPlan.IsExactShortfall(before,needs,remote,belt)||!RebirthRemoteResourceCraftPlan.TryDebit(before,needs,remote,belt,out _,out _))return false;
        return Publish(journal,operation,original,bytes);
    }
    internal static bool HasSavedPersonalAdmission(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root)
    {
        try
        {
            if(!Scope(actor,original)&&!RebirthRemoteResourceCraftAdoption.HostScope(actor,original)||root==null||
                !RebirthRemoteResourceCraftOriginal.ValidateInput(original,out _,out _,out _,out _,out _)||
                RebirthRemoteResourceFetchedStationOriginal.IsCandidate(original)||RebirthRemoteResourcePlacedStationOriginal.IsCandidate(original)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world)||
                root.World!=world||root.Owner!=owner.StorageKey||root.Creation!=original.Creation||
                (int)root.Copy().Attribute("kind")!=(int)RemoteResourceClientOperation.Craft||(int)root.Copy().Attribute("deaths")!=original.Deaths||
                RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind)!=root.Operation)return false;
            var record=Admissions().Read(root.Operation);
            byte[] expected=Encoding.UTF8.GetBytes(new XElement("craftSourceAdmission",new XAttribute("originalDigest",original.Digest),original.Copy()).ToString(SaveOptions.DisableFormatting));
            return record?.State==RebirthCamperTransferJournal.Phase.Applied&&record.Operation==root.Operation&&record.Vehicle==original.Operation&&record.OwnerCreation==original.Creation&&
                record.Revision==0&&record.Before.SequenceEqual(expected)&&record.After.SequenceEqual(expected);
        }
        catch{return false;}
    }
    internal static bool TryReadSavedPersonalAdmission(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,out RebirthRemoteResourceClientOriginal.Intent original)
    {
        original=null;
        try
        {
            if(root==null)return false;var record=Admissions().Read(root.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||!record.Before.SequenceEqual(record.After)||record.Before.Length>17*1024*1024)return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),17*1024*1024);
            if(body.Name!="craftSourceAdmission"||body.Attributes().Count()!=1||body.Elements().Count()!=1||
                !RebirthRemoteResourceClientOriginal.TryDecodeOriginal(body.Element("fetchClientOriginal")?.ToString(SaveOptions.DisableFormatting),out var decoded)||
                (string)body.Attribute("originalDigest")!=decoded.Digest||!HasSavedPersonalAdmission(actor,decoded,root))return false;
            original=decoded;return true;
        }
        catch{return false;}
    }
    internal static bool CanPublishPlannedOwnerAction(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,ItemStack[] actualPlannedPaid)
    {
        try
        {
            var record=Admissions().Read(root.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||!record.Before.SequenceEqual(record.After))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),17*1024*1024);
            if(body.Name!="craftSourceAdmission"||body.Attributes().Count()!=1||body.Elements().Count()!=1||
                !RebirthRemoteResourceClientOriginal.TryDecodeOriginal(body.Element("fetchClientOriginal")?.ToString(SaveOptions.DisableFormatting),out var original)||
                record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||(string)body.Attribute("originalDigest")!=original.Digest||!Scope(actor,original)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||root.Owner!=owner.StorageKey||root.Creation!=original.Creation||
                RebirthRemoteResourceDurableJournal.Operation(root.World,root.Owner,root.Creation,original.Epoch,original.Request,original.Kind)!=root.Operation)return false;
            return RebirthRemoteResourceCraftOriginal.Canonical(original,root.World,root.Operation,root.Binding,actualPlannedPaid,out _);
        }
        catch{return false;}
    }
    internal static void Request(EntityPlayer actor,string text)
    {
        try
        {
            bool local=actor is EntityPlayerLocal primary&&actor.world?.IsRemote()==false&&
                ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetPrimaryPlayer(),primary)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor);
            if(!local&&!RebirthRemoteResourceRepairProtocol.SupportsServerActor(actor))return;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,24*1024*1024);
            if(body.Name!="craftRequest"||body.Attributes().Count()!=2||(int?)body.Attribute("version")!=2||body.Elements().Count()!=2||body.Element("ownerActionBody")==null)return;
            int stage=(int)body.Attribute("stage");if(stage<0||stage>8)return;
            RebirthRemoteResourceClientOriginal.Intent original;
            if(body.Element("fetchClientOriginal")!=null)
            {if(!RebirthRemoteResourceClientOriginal.TryDecodeOriginal(body.Element("fetchClientOriginal").ToString(SaveOptions.DisableFormatting),out original))return;}
            else
            {
                var reference=body.Element("originalRef");if(reference==null||reference.Attributes().Count()!=4||reference.Nodes().Any()||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string worldId))return;
                Guid operation=RebirthRemoteResourceDurableJournal.Operation(worldId,owner.StorageKey,RebirthCamperCharacterAccess.CurrentCreation(actor),(ulong)reference.Attribute("epoch"),(ulong)reference.Attribute("request"),RemoteResourceClientOperation.Craft);
                var record=Admissions().Read(operation);if(record==null||record.State!=RebirthCamperTransferJournal.Phase.Applied||!record.Before.SequenceEqual(record.After))return;
                var admission=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),17*1024*1024);
                if(!RebirthRemoteResourceClientOriginal.TryDecodeOriginal(admission.Element("fetchClientOriginal")?.ToString(SaveOptions.DisableFormatting),out original)||original.Operation.ToString("N")!=(string)reference.Attribute("operation")||original.Digest!=(string)reference.Attribute("digest"))return;
            }
            if(!Admit(actor,original))return;
            if(actor.Died>original.Deaths)
            {
                bool refundPermission=false;
                if(stage==5&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var historicalOwner)&&RemoteResourceWorldIdentity.TryGet(actor.world,out string historicalWorld))
                {
                    var historicalOperation=RebirthRemoteResourceDurableJournal.Operation(historicalWorld,historicalOwner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
                    if(RebirthRemoteResourceDurableJournal.TryRead(historicalOwner.StorageKey,historicalOperation,out var noPayment,out bool noPaymentUnknown)&&!noPaymentUnknown&&noPayment!=null)
                        RebirthRemoteResourceDurableOutcome.AcknowledgePersonalCraftNoPayment(actor,original,historicalOperation,noPayment.Binding);
                }
                if(stage==6||stage==7)RebirthRemoteResourceRepairRefund.Host(actor,original,RebirthCookingRemoteAdmission.ReadProtocolXml(body.Element("ownerActionBody").Value,24*1024*1024),stage==7,out refundPermission);
                if(RebirthRemoteResourceCraftAdoption.Read(actor,original,out var historical))Reply(actor,original,historical,refundPermission);
                return;
            }
            var remote=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("remoteRequirements").Value));
            bool permission=false;string continuation=null;
            if(!RebirthRemoteResourceDurableDispatch.Resolve(actor,original.Epoch,original.Request,original.Kind,remote,stage!=0,out var result)||result==null)
            {Reply(actor,original,null,false);return;}
            if(stage==1&&RebirthRemoteResourceCraftSettlement.CanonicalAction(original,result,out var action))RebirthRemoteResourceCraftSettlement.Prepare(actor,original,action,out permission);
            else if(stage==2)RebirthRemoteResourceCraftSettlement.ObserveUsed(actor,original);
            else if(stage==8&&RebirthRemoteResourceCraftSettlement.CanonicalAction(original,result,out var continued)&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var continuedOwner))
                RebirthRemoteResourceOwnerAction.PrepareCraftContinuation(actor,continuedOwner,original,result,continued,out continuation);
            else if(stage==3||stage==4)RebirthRemoteResourceCraftSettlement.Reject(actor,original,stage==4);
            else if(stage==5)RebirthRemoteResourceDurableOutcome.AcknowledgeNoPayment(actor,result.Operation,result.Binding);
            else if(stage==6||stage==7)RebirthRemoteResourceRepairRefund.Host(actor,original,RebirthCookingRemoteAdmission.ReadProtocolXml(body.Element("ownerActionBody").Value,24*1024*1024),stage==7,out permission);
            if(!RebirthRemoteResourceDurableDispatch.Resolve(actor,original.Epoch,original.Request,original.Kind,remote,true,out result)||result==null)return;
            Reply(actor,original,result,permission,continuation);
        }
        catch{/* Unknown is not a refusal or payment. */}
    }
    private static void Reply(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record result,bool permission,string continuation=null)
    {
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string worldId))return;
        var root=result?.Copy();Guid source=result?.Operation??RebirthRemoteResourceDurableJournal.Operation(worldId,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
        var body=new XElement("craftResult",new XAttribute("version",2),new XAttribute("original",original.Operation.ToString("N")),new XAttribute("originalDigest",original.Digest),
            new XAttribute("sourceWorld",worldId),new XAttribute("sourceOperation",source.ToString("N")),new XAttribute("binding",result?.Binding??""),new XAttribute("phase",result?.Phase??0),new XAttribute("outcome",(int?)root?.Attribute("outcome")??0),
            new XAttribute("permission",permission?1:0),new XAttribute("continuationProof",continuation??""),new XAttribute("rejecting",(int?)root?.Attribute("ownerRejecting")??0),new XAttribute("ownerProof",(string)root?.Attribute("ownerProof")??""),new XAttribute("disposition",(string)root?.Attribute("disposition")??""),
            new XAttribute("noUseProof",(string)root?.Attribute("ownerNoUseProof")??""),new XElement("paid",root?.Element("removed")?.Value??Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(Array.Empty<ItemStack>()))));
        if(actor is EntityPlayerLocal primary&&actor.world?.IsRemote()==false&&ReferenceEquals(actor.world.GetPrimaryPlayer(),primary)&&
            ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor))
            Result(primary,original.Operation,body.ToString(SaveOptions.DisableFormatting));
        else NetPackageRebirthRemoteRepairResult.Send(actor.entityId,original,body.ToString(SaveOptions.DisableFormatting));
    }
    internal static void Result(EntityPlayerLocal player,Guid operation,string text)
    {
        try
        {
            if(!pending.TryGetValue(operation,out var value)||!Current(player,value))return;var original=value.Original;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,12*1024*1024);
            if(body.Name!="craftResult"||body.Attributes().Count()!=14||body.Elements().Count()!=1||(int?)body.Attribute("version")!=2||(string)body.Attribute("original")!=operation.ToString("N")||
                (string)body.Attribute("originalDigest")!=original.Digest||!Guid.TryParseExact((string)body.Attribute("sourceOperation"),"N",out var source)||!Ack(original,(string)body.Attribute("sourceWorld"),source))return;
            value.Ack=true;bool Auth()=>Current(player,value);int phase=(int)body.Attribute("phase"),outcome=(int)body.Attribute("outcome");string binding=(string)body.Attribute("binding"),proof=(string)body.Attribute("ownerProof");
            if(phase==0&&outcome==0)return;
            if(phase==3&&outcome==2&&string.IsNullOrEmpty((string)body.Attribute("noUseProof"))){Send(player,value,5);return;}
            if(phase==4&&outcome==2&&(string)body.Attribute("disposition")=="unused-no-payment")
            {if(RebirthRemoteResourceClientOriginal.SaveNoPaymentTerminal(player,original,(string)body.Attribute("sourceWorld"),source,binding,proof,Auth))pending.Remove(operation);return;}
            if(phase==4&&(outcome==1||outcome==2)&&(string)body.Attribute("disposition")=="refunded")
            {
                if(RebirthRemoteResourceRepairRefund.Complete(player,original,source,binding,proof,Auth)&&RebirthRemoteResourceCraftOriginal.CompleteIfPresent(original))
                {string receipt=RebirthRemoteResourceOwnerAction.Receipt(RebirthRemoteResourceRepairRefund.Child(original));player.Buffs.SetCustomVar(receipt,0,true);player.Buffs.RemoveCustomVar(receipt);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0);pending.Remove(operation);}return;
            }
            if(phase==4&&outcome==1&&(string)body.Attribute("disposition")=="used")
            {
                if(!RebirthRemoteResourceCraftOriginal.TryRead(original,out var action,out _)||proof!=RebirthRemoteResourceCraftSettlement.UsedProof(original,source,binding,action))return;
                if(RebirthRemoteResourceClientOriginal.SaveCraftTerminal(player,original,source,binding,proof,action.Element("markedRecipe").Value,(int)action.Attribute("count"),(int)action.Attribute("quality"),RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value),Auth)&&
                    RebirthRemoteResourceCraftOriginal.Complete(original)&&
                    (player.Died==original.Deaths||RebirthRemoteResourceClientOriginal.CompleteCraftTerminal(original,source,binding)&&
                        RebirthRemoteResourceOwnerAction.ReleaseHistoricalCraftTerminal(player,original,source,binding,proof)))pending.Remove(operation);return;
            }
            if((phase!=2||outcome!=1)&&!(phase==3&&outcome==2&&!string.IsNullOrEmpty((string)body.Attribute("noUseProof"))))return;
            var paid=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(body.Element("paid").Value));
            if(!RebirthRemoteResourceClientOriginal.SaveCraftSource(player,original,(string)body.Attribute("sourceWorld"),source,binding,paid,Auth))return;
            value.Source=source;value.SourceWorld=(string)body.Attribute("sourceWorld");value.Binding=binding;value.Paid=ItemStack.Clone(paid);
            string noUse=(string)body.Attribute("noUseProof");
            if(!string.IsNullOrEmpty(noUse))
            {
                if(!RebirthRemoteResourceRepairRefund.AcceptNoUse(player,original,source,binding,noUse,Auth))return;value.NoUse=noUse;
                if(value.Refund!=null&&(int)body.Attribute("permission")==1&&RebirthRemoteResourceRepairRefund.Apply(player,original,value.Refund,Auth))
                {value.RefundApplied=true;Send(player,value,7,value.Refund.ToString(SaveOptions.DisableFormatting));}return;
            }
            if(player.Died>original.Deaths)return; // Status only; never resume an old-life native entry.
            if((int)body.Attribute("rejecting")==1)
            {value.Rejecting=RebirthRemoteResourceCraftOriginal.SaveRejection(original);if(value.Rejecting&&RebirthRemoteResourceCraftOriginal.SaveOwnerFive(player,original))Send(player,value,4);return;}
            if(!RebirthRemoteResourceCraftOriginal.TryRead(original,out var retained,out var state))
            {
                if(value.Entry==null||!RebirthRemoteResourceCraftOriginal.Prepare(player,original,value.SourceWorld,source,binding,paid,out retained))
                {value.Rejecting=RebirthRemoteResourceCraftOriginal.SaveRejection(original);if(value.Rejecting)Send(player,value,3);return;}
                state=RebirthCamperTransferJournal.Phase.Prepared;
            }
            if(RebirthRemoteResourceCraftOriginal.ObserveLocalUsage(player,original))
            {RebirthRemoteResourceOwnerAction.TryHoldRestoredCraft(player,original,Auth);SaveNative(player,original,2);Send(player,value,2);return;}
            var before=RebirthRemoteResourceClientOriginal.ReadActor(retained.Element("beforeActor").Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(retained.Element("afterActor").Value);
            if(state==RebirthCamperTransferJournal.Phase.Uncertain)
            {
                if(!RebirthRemoteResourceOwnerAction.TryHold(player,original,source,binding,before,after,Auth))
                    RebirthRemoteResourceOwnerAction.TryHoldMixedCraft(player,original,Auth);
                string continuationProof=(string)body.Attribute("continuationProof");
                if(!string.IsNullOrEmpty(continuationProof)&&RebirthRemoteResourceOwnerAction.CompleteAcceptedCraftCost(player,original,source,binding,continuationProof,Auth))Send(player,value,2);
                else if(RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value).Matches(RebirthCamperEnergyActorImage.Capture(player))&&
                    RebirthRemoteResourceClientOriginal.QueueImage(player.PlayerUI.xui.GetCraftingData())==original.Copy().Element("queueBefore").Value)
                {value.Rejecting=RebirthRemoteResourceCraftOriginal.SaveRejection(original);if(value.Rejecting)Send(player,value,3);}
                else Send(player,value,8);
                return;
            }
            if(!RebirthRemoteResourceOwnerAction.TryHold(player,original,source,binding,before,after,Auth))return;
            if(state!=RebirthCamperTransferJournal.Phase.Prepared)
            {
                // Never replay an uncertain callback. A positive full native
                // Before + full queue Before can still prove unused; any
                // partial effect keeps this original unknown and held.
                value.Rejecting=RebirthRemoteResourceCraftOriginal.SaveRejection(original);if(value.Rejecting)Send(player,value,3);return;
            }
            if((int)body.Attribute("permission")!=1){SaveNative(player,original,1);Send(player,value,1);return;}
            if(value.Entry!=null&&RebirthRemoteResourceCraftOriginal.Expected(original,paid,source,binding,out _,out _,out var marked,out int count,out _)&&
                RebirthRemoteResourceCraftRecipe.TryUnmarked(marked,source,binding,out var expected)&&
                RemoteResourceClientTransactionCoordinator.ResumeOriginalCraft(value.Entry,original,source,binding,paid,expected,count,after,Auth,out _))
            {value.Used=true;Send(player,value,2);return;}
            if(RebirthRemoteResourceCraftOriginal.TryRead(original,out _,out var attempted)&&attempted==RebirthCamperTransferJournal.Phase.Uncertain)
            {Send(player,value,8);return;}
            value.Rejecting=RebirthRemoteResourceCraftOriginal.SaveRejection(original);if(value.Rejecting)Send(player,value,3);
        }
        catch{/* Keep original claims and local action journal on ambiguity. */}
    }
}
