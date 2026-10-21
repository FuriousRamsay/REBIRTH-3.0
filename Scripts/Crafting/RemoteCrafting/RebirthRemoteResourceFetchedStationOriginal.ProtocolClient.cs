using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    private sealed class ClientRequest{internal RebirthRemoteResourceClientOriginal.Intent Original;internal float Next;internal int Stage;internal XElement Refund;}
    private static readonly Dictionary<Guid,ClientRequest> ClientRequests=new Dictionary<Guid,ClientRequest>();
    private static readonly Dictionary<Guid,ClientRequest> EndedQueries=new Dictionary<Guid,ClientRequest>();
    private static bool EndedZeroClientScope(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)=>ThreadManager.IsMainThread()&&player?.world?.IsRemote()==true&&
        player.IsSpawned()&&!player.IsDead()&&ReferenceEquals(GameManager.Instance?.World,player.world)&&ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&ReferenceEquals(player.world.GetEntity(player.entityId),player)&&
        IsCandidate(original)&&original.World==player.world.worldState.Guid&&player.Died>original.Deaths&&original.Creation==RebirthCamperCharacterAccess.CurrentCreation(player)&&
        RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)&&owner.CanonicalId==original.Owner;
    private static World clientWorld;private static string clientDirectory;private static float clientScan;
    private static void BindClientWorld(World world)
    {if(!ReferenceEquals(clientWorld,world)||clientDirectory!=GameIO.GetSaveGameDir()){ClientRequests.Clear();EndedQueries.Clear();clientWorld=world;clientDirectory=GameIO.GetSaveGameDir();clientScan=0;}}
    internal static bool CancelClientOriginal(EntityPlayerLocal player,Guid job)
    {
        if(!ProtocolActive||!ClientRequests.TryGetValue(job,out var pending)||!RebirthRemoteResourceClientOriginal.Current(player,pending.Original))return false;
        bool paid=RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(pending.Original,out _,out _,out _,out _);
        if(!paid&&!RebirthRemoteResourcePreAdmissionRefusal.RequestClient(player,pending.Original))return false;
        pending.Stage=paid?3:7;pending.Next=0;return true;
    }
    internal static bool BeginClient(EntityPlayerLocal player,ItemActionEntryCraft entry,ulong epoch,ulong request,List<ItemStack> full,List<ItemStack> remote)
    {
        try
        {
            BindClientWorld(player?.world);
            if(!ProtocolActive||!RebirthRemoteResourceClientOriginal.TryReadPending(out _))return false;
            if(TryAttachClient(player,entry))return true;
            if(!ProtocolActive||!RebirthRemoteResourceRepairProtocol.SupportsClientTransport()||ClientRequests.Count>=32||
                !Capture(player,entry,epoch,request,full,remote,out var original)||!RebirthRemoteResourceOwnerAction.TryHoldPreparedOriginal(player,original))return false;
            ClientRequests.Add(original.Operation,new ClientRequest{Original=original});
            player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation),10f,true);
            GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
        }
        catch{return false;}
    }
    internal static bool TryAttachOwningEntry(EntityPlayerLocal player,ItemActionEntryCraft entry)
        =>ProtocolActive&&TryAttachClient(player,entry);
    internal static bool IsEntryCandidate(EntityPlayerLocal player,ItemActionEntryCraft entry)
    {
        var window=entry?.ItemController?.GetParentByType<XUiC_WorkstationWindowGroup>();
        var recipe=(entry?.ItemController as XUiC_RecipeEntry)?.Recipe;
        return player?.world?.IsRemote()==true&&!RebirthSurvivorMode.IsEnabledForCurrentWorld()&&recipe!=null&&!recipe.IsScrap&&!recipe.materialBasedRecipe&&
            !RebirthCookingHeat.Managed(recipe)&&!RebirthRecyclerRuntime.IsMarked(recipe)&&window!=null&&window.inputWindow==null&&
            RebirthCamperStationWindowSession.TryGetCurrent(player,out var station,out _)&&ReferenceEquals(window.WorkstationData?.TileEntity,station)&&
            RebirthStationPlayerIngredients.UsesPlayerPayment(player,station);
    }
    private static bool TryAttachClient(EntityPlayerLocal player,ItemActionEntryCraft entry)
    {
        try
        {
            if(!IsEntryCandidate(player,entry)||!RebirthRemoteResourceClientOriginal.TryReadPending(out var originals)||
                !RebirthCamperStationWindowSession.TryGetCurrent(player,out _,out var address))return false;
            var recipe=(entry.ItemController as XUiC_RecipeEntry)?.Recipe;if(recipe==null)return false;
            foreach(var original in originals)
            {
                if(!IsCandidate(original)||!RebirthRemoteResourceClientOriginal.Current(player,original))continue;
                var action=original.Copy().Element("action");
                if((string)action.Attribute("station")!=address.ToString()||(int?)action.Attribute("count")!=entry.craftCountControl.Count||
                    (int?)action.Attribute("tier")!=entry.craftingTier||action.Element("baseRecipe")?.Value!=RebirthRemoteResourceOwnerAction.RecipeImage(recipe))continue;
                if(!ClientRequests.ContainsKey(original.Operation))
                {
                    if(ClientRequests.Count>=32)return true;
                    ClientRequests.Add(original.Operation,new ClientRequest{Original=original,
                        Stage=RebirthRemoteResourcePreAdmissionRefusal.ClientRequested(original)?7:RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out _,out _,out _)?1:0});
                }
                return true;
            }
            return false;
        }
        catch{return false;}
    }
    private static void ProtocolUpdate(ref ModEvents.SGameUpdateData data)
    {
        if(!ProtocolActive)return;var world=GameManager.Instance?.World;
        BindClientWorld(world);
        if(world?.IsRemote()!=true)return;var player=world.GetPrimaryPlayer();if(player==null||player.IsDead()||!player.IsSpawned())return;
        float now=Time.realtimeSinceStartup;
        if(now>=clientScan)
        {
            clientScan=now+5;
            if(RebirthRemoteResourceClientOriginal.TryReadPending(out var originals))foreach(var original in originals)
            {
                if(EndedZeroClientScope(player,original))
                {
                    // Transient requests are not debt evidence. Old originals
                    // and their financial histories remain on disk unchanged.
                    ClientRequests.Remove(original.Operation);
                    if(EndedQueries.Count<32&&!EndedQueries.ContainsKey(original.Operation)&&!RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out _,out _,out _))
                        EndedQueries.Add(original.Operation,new ClientRequest{Original=original,Stage=8});
                    continue;
                }
                if(!IsCandidate(original)||!RebirthRemoteResourceClientOriginal.Current(player,original)||ClientRequests.ContainsKey(original.Operation)||ClientRequests.Count>=32)continue;
                // Re-query the original even if cold owner custody is unknown.
                // Never replay a callback or infer a refund from current images.
                ClientRequests.Add(original.Operation,new ClientRequest{Original=original,
                    Stage=RebirthRemoteResourcePreAdmissionRefusal.ClientRequested(original)?7:RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out _,out _,out _)?1:0});
                if(!RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out _,out _,out _)&&
                    RebirthRemoteResourceOwnerAction.TryHoldPreparedOriginal(player,original))
                {player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation),10f,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);}
            }
        }
        foreach(var ended in EndedQueries.Values.ToArray())
        {
            if(now<ended.Next||!EndedZeroClientScope(player,ended.Original))continue;
            ended.Next=now+5;var persistent=GameManager.Instance.GetPersistentLocalPlayer();if(persistent?.PrimaryId==null)continue;
            var query=new XElement("fetchedStationRequest",new XAttribute("version",1),new XAttribute("stage",ended.Stage),ended.Original.Copy());
            NetPackageRebirthRemoteRepairOriginal.Send(player.entityId,persistent.PrimaryId,ended.Original,query.ToString(SaveOptions.DisableFormatting));
            // No new-life native10, upload, hold, callback, or financial ACK.
        }
        foreach(var request in ClientRequests.Values.ToArray())
        {
            if(RebirthRemoteResourcePreAdmissionRefusal.HasClientTerminal(request.Original)){ClientRequests.Remove(request.Original.Operation);continue;}
            if(now<request.Next||!RebirthRemoteResourceClientOriginal.Current(player,request.Original))continue;
            request.Next=now+1;var persistent=GameManager.Instance.GetPersistentLocalPlayer();if(persistent?.PrimaryId==null)continue;
            if(request.Stage==0&&!RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(request.Original,out _,out _,out _,out _)&&
                RebirthRemoteResourceOwnerAction.TryHoldPreparedOriginal(player,request.Original))
            {player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(request.Original.Operation),10f,true);GameManager.Instance.SaveLocalPlayerData();}
            GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
            XElement originalNode=request.Original.Copy();
            bool refundStage=request.Stage==5||request.Stage==6;
            if(refundStage)
            {
                if(request.Refund==null||!RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(request.Original,out _,out var source,out _,out _))continue;
                originalNode=new XElement("originalRef",new XAttribute("operation",request.Original.Operation.ToString("N")),new XAttribute("source",source.ToString("N")),new XAttribute("digest",request.Original.Digest));
            }
            var body=new XElement("fetchedStationRequest",new XAttribute("version",1),new XAttribute("stage",request.Stage),originalNode,refundStage?new XElement(request.Refund):null);
            NetPackageRebirthRemoteRepairOriginal.Send(player.entityId,persistent.PrimaryId,request.Original,body.ToString(SaveOptions.DisableFormatting));
        }
    }
    internal static bool ValidateEndedClientTerminal(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,string text)
    {
        try
        {
            if(!EndedZeroClientScope(player,original)||!EndedClientEffectsAbsent(original)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,8192);
            bool Hex(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
            string world=(string)body.Attribute("sourceWorld"),binding=(string)body.Attribute("binding");
            return body.Name=="fetchedStationEndedZero"&&body.Attributes().Count()==9&&!body.Nodes().Any()&&(int?)body.Attribute("version")==1&&
                (int?)body.Attribute("phase")==4&&(string)body.Attribute("original")==original.Operation.ToString("N")&&(string)body.Attribute("digest")==original.Digest&&
                Hex(world)&&Hex(binding)&&Hex((string)body.Attribute("nativeRejected"))&&Guid.TryParseExact((string)body.Attribute("source"),"N",out var source)&&
                source==RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind)&&
                (string)body.Attribute("terminalProof")==RebirthRemoteResourceDurableJournal.Digest("authenticated-no-payment|"+owner.StorageKey+"|"+binding);
        }
        catch{return false;}
    }
    internal static void EndedZeroResult(EntityPlayerLocal player,Guid job,string text)
    {
        try
        {
            if(!ProtocolActive||!EndedQueries.TryGetValue(job,out var request)||!EndedZeroClientScope(player,request.Original))return;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,8192);var original=request.Original;
            bool Hex(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
            if(body.Name!="fetchedStationEndedZero"||body.Attributes().Count()!=9||body.Nodes().Any()||(int?)body.Attribute("version")!=1||
                (string)body.Attribute("original")!=job.ToString("N")||(string)body.Attribute("digest")!=original.Digest||!Hex((string)body.Attribute("sourceWorld"))||
                !Hex((string)body.Attribute("binding"))||!Hex((string)body.Attribute("nativeRejected"))||
                !Guid.TryParseExact((string)body.Attribute("source"),"N",out var source)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||
                source!=RebirthRemoteResourceDurableJournal.Operation((string)body.Attribute("sourceWorld"),owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind)||
                ((int?)body.Attribute("phase")!=3&&(int?)body.Attribute("phase")!=4))return;
            if((int)body.Attribute("phase")==3)
            {if((string)body.Attribute("terminalProof")!="")return;request.Stage=9;request.Next=0;return;}
            if(ValidateEndedClientTerminal(player,original,text)&&RebirthRemoteResourceClientOriginal.CompleteEndedFetchedNoEffect(player,original,text))EndedQueries.Remove(job);
        }
        catch{}
    }
    internal static void Result(EntityPlayerLocal player,Guid job,string text)
    {
        if(!ProtocolActive||!ClientRequests.TryGetValue(job,out var pending)||!RebirthRemoteResourceClientOriginal.Current(player,pending.Original))return;
        try
        {
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,12*1024*1024);var original=pending.Original;
            if(body.Name!="fetchedStationResult"||body.Attributes().Count()!=14||body.Elements().Count()!=1||body.Element("paid")==null||(int?)body.Attribute("version")!=1||
                (string)body.Attribute("original")!=job.ToString("N")||(string)body.Attribute("digest")!=original.Digest||
                !Guid.TryParseExact((string)body.Attribute("source"),"N",out var source)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||
                source!=RebirthRemoteResourceDurableJournal.Operation((string)body.Attribute("sourceWorld"),owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind))return;
            int phase=(int)body.Attribute("phase"),outcome=(int)body.Attribute("outcome");string binding=(string)body.Attribute("binding"),proof=(string)body.Attribute("proof");
            bool Auth()=>ProtocolActive&&ClientRequests.TryGetValue(job,out var current)&&ReferenceEquals(current,pending)&&RebirthRemoteResourceClientOriginal.Current(player,original);
            if(phase==3&&outcome==2){pending.Stage=2;return;}
            if(phase==4&&outcome==2&&(string)body.Attribute("disposition")=="unused-no-payment")
            {if(RebirthRemoteResourceClientOriginal.SaveNoPaymentTerminal(player,original,(string)body.Attribute("sourceWorld"),source,binding,proof,Auth))ClientRequests.Remove(job);return;}
            if(phase<2||outcome!=1)return;
            var paid=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(body.Element("paid").Value));
            if(!RebirthRemoteResourceClientOriginal.SaveCraftSource(player,original,(string)body.Attribute("sourceWorld"),source,binding,paid,Auth))return;
            if(phase==4&&(string)body.Attribute("disposition")=="refunded")
            {if(RebirthRemoteResourceRepairRefund.Complete(player,original,source,binding,proof,Auth))ClientRequests.Remove(job);return;}
            if((int?)body.Attribute("rejecting")==1)
            {
                string noUse=(string)body.Attribute("noUseProof");
                if(string.IsNullOrEmpty(noUse))
                {if(SaveClientRejection(player,original,source,binding,Auth))pending.Stage=4;return;}
                if(!RebirthRemoteResourceRepairRefund.AcceptNoUse(player,original,source,binding,noUse,Auth)||
                    !RebirthRemoteResourceRepairRefund.TryPrepare(player,original,source,binding,noUse,paid,Auth,out var refund))return;
                pending.Refund=refund;
                bool Permission()=>Auth()&&(bool?)body.Attribute("refundPermission")==true&&
                    (string)body.Attribute("refundDigest")==RebirthRemoteResourceDurableJournal.Digest(refund.ToString(SaveOptions.DisableFormatting));
                if(Permission())RebirthRemoteResourceRepairRefund.Apply(player,original,refund,Permission);
                pending.Stage=RebirthRemoteResourceRepairRefund.IsApplied(player,original,refund)?6:5;return;
            }
            if(pending.Stage==7||pending.Stage==3)GameManager.ShowTooltip(player,"Crafting is already processing. Cancel it from the station queue.");
            pending.Stage=1;
            if(phase==4&&(string)body.Attribute("disposition")=="used"&&CompleteClientUsed(player,original,source,(string)body.Attribute("sourceWorld"),binding,proof,Auth))ClientRequests.Remove(job);
        }
        catch{/* Keep original and paid claim on unknown payloads. */}
    }
}
