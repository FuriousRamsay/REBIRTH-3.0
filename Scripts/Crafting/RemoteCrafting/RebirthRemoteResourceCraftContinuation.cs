using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Separate accepted-card permission. Mixed custody alone grants no setters.
internal static partial class RebirthRemoteResourceOwnerAction
{
    internal static string ReadCraftContinuationAuditProof(RebirthRemoteResourceDurableJournal.Record root)
    {
        try
        {
            var wrapper=root?.Copy().Element("ownerAction");
            if((string)wrapper?.Attribute("domain")!="native-craft"||!Guid.TryParseExact((string)wrapper.Attribute("child"),"N",out var child))return "";
            var journal=new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteCraftAcceptedContinuation"));
            var record=journal.Read(child);if(record==null)return "";
            if(record.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=child||record.OwnerCreation!=root.Creation||record.Revision!=0||
                record.Before.Length>24*1024*1024||!record.Before.SequenceEqual(record.After))return "unknown";
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),24*1024*1024);
            if(body.Name!="acceptedCraft"||body.Attributes().Count()!=3||body.Elements().Count()!=3||
                (string)body.Attribute("source")!=root.Operation.ToString("N")||(string)body.Attribute("binding")!=root.Binding||
                body.Element("actionDigest")?.Value!=RebirthRemoteResourceDurableJournal.Digest(wrapper.Element("original").Value))return "unknown";
            return RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(record.Before));
        }
        catch{return "unknown";}
    }
    private static bool CraftMixture(RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,RebirthCamperEnergyActorImage current)
    {
        if(before==null||after==null||current==null||!DebitOnly(before.Bag,after.Bag)||!DebitOnly(before.Belt,after.Belt)||
            !Same(before.Cursor,after.Cursor)||!Same(current.Cursor,before.Cursor)||before.BagLocks==null||after.BagLocks==null||current.BagLocks==null||
            !before.BagLocks.SequenceEqual(after.BagLocks)||!before.BagLocks.SequenceEqual(current.BagLocks))return false;
        foreach(var area in new[]{new[]{before.Bag,after.Bag,current.Bag},new[]{before.Belt,after.Belt,current.Belt}})
        {
            if(area[0].Length!=area[1].Length||area[0].Length!=area[2].Length)return false;
            for(int i=0;i<area[0].Length;i++)if(!Same(area[2][i],area[0][i])&&!Same(area[2][i],area[1][i]))return false;
        }
        return true;
    }
    private static bool AcceptedCraftImage(RebirthRemoteResourceClientOriginal.Intent original,XElement action,Guid source,string binding,string recipe)
    {
        if(recipe==action.Element("markedRecipe").Value)return true;
        return RebirthRemoteResourceCraftRecipe.TryRead(original.Copy().Element("action").Element("template").Value,out var initial)&&
            RebirthRemoteResourceCraftRecipe.TryMarked(initial,source,binding,out var marked)&&RecipeImage(marked)==recipe;
    }
    internal static bool PrepareCraftContinuation(EntityPlayer actor,RebirthStablePlayerIdentity owner,RebirthRemoteResourceClientOriginal.Intent original,
        RebirthRemoteResourceDurableJournal.Record root,XElement action,out string proof)
    {
        proof=null;
        try
        {
            if(owner==null||root?.Phase!=2||root.Owner!=owner.StorageKey||root.Creation!=original?.Creation||
                !RemoteResourceWorldIdentity.TryGet(actor?.world,out string sourceWorld)||root.World!=sourceWorld||
                root.Operation!=RebirthRemoteResourceDurableJournal.Operation(root.World,root.Owner,root.Creation,original.Epoch,original.Request,RemoteResourceClientOperation.Craft)||!RebirthRemoteResourceCraftSettlement.MatchesRootAction(original,root)||
                ((int?)root.Copy().Attribute("ownerRejecting")??0)!=0||root.Copy().Attribute("ownerNoUseProof")!=null||root.Copy().Element("ownerRefund")!=null||
                !RebirthRemoteResourceCraftSettlement.CanonicalAction(original,root,out var canonical)||!XNode.DeepEquals(action,canonical)||
                (!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||!actor.IsSpawned()||actor.Died!=original.Deaths||actor.world.worldState.Guid!=original.World||!ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.StorageKey!=owner.StorageKey||identity.CanonicalId!=original.Owner||!RebirthOriginalActionScope.Matches(actor,owner.StorageKey,original.Creation)))return false;
            if(!RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var saved,out var captured)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,Receipt(original.Operation),out float phase,out bool present)||!present||phase!=1f)return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor").Value);
            var after=RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value);
            if(!CraftMixture(before,after,actual)||saved.craftingData?.RecipeQueueItems==null||saved.craftingData.RecipeQueueItems.Length>255)return false;
            var rows=saved.craftingData.RecipeQueueItems.Where(row=>row?.Recipe!=null&&IsMarked(row.Recipe,root.Operation,root.Binding)).ToArray();
            if(rows.Length!=1||rows[0].RepairItem!=null&&rows[0].RepairItem.type!=0||rows[0].AmountToRepair!=0||rows[0].Multiplier!=(int)action.Attribute("count")||rows[0].Quality!=(int)action.Attribute("quality"))return false;
            string recipe=SavedRecipeImage(rows[0].Recipe,captured);
            if(!AcceptedCraftImage(original,action,root.Operation,root.Binding,recipe))return false;
            var journal=new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteCraftAcceptedContinuation"));
            var retained=journal.Read(original.Operation);
            if(retained==null)
            {
                var evidence=new XElement("acceptedCraft",new XAttribute("original",original.Digest),new XAttribute("source",root.Operation.ToString("N")),new XAttribute("binding",root.Binding),
                    new XElement("actionDigest",RebirthRemoteResourceDurableJournal.Digest(action.ToString(SaveOptions.DisableFormatting))),new XElement("acceptedRecipe",recipe),new XElement("savedActor",RebirthRemoteResourceClientOriginal.ActorImage(actual)));
                byte[] bytes=Encoding.UTF8.GetBytes(evidence.ToString(SaveOptions.DisableFormatting));
                if(bytes.Length>24*1024*1024)return false;
                retained=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            }
            if(retained.Before.Length>24*1024*1024||retained.Revision!=0||retained.Vehicle!=original.Operation||retained.OwnerCreation!=original.Creation||!retained.Before.SequenceEqual(retained.After))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(retained.Before),24*1024*1024);
            if(body.Name!="acceptedCraft"||body.Attributes().Count()!=3||body.Elements().Count()!=3||(string)body.Attribute("original")!=original.Digest||
                (string)body.Attribute("source")!=root.Operation.ToString("N")||(string)body.Attribute("binding")!=root.Binding||body.Element("actionDigest")?.Value!=RebirthRemoteResourceDurableJournal.Digest(action.ToString(SaveOptions.DisableFormatting))||
                !AcceptedCraftImage(original,action,root.Operation,root.Binding,body.Element("acceptedRecipe")?.Value)||
                !CraftMixture(before,after,RebirthRemoteResourceClientOriginal.ReadActor(body.Element("savedActor")?.Value)))return false;
            if(retained.State==RebirthCamperTransferJournal.Phase.Prepared)retained=journal.Advance(original.Operation,retained.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(retained.State==RebirthCamperTransferJournal.Phase.Uncertain)retained=journal.Advance(original.Operation,retained.State,RebirthCamperTransferJournal.Phase.Applied);
            if(retained.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
            proof=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(retained.Before));return true;
        }
        catch{proof=null;return false;}
    }
    internal static bool CompleteAcceptedCraftCost(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof,Func<bool> authenticatedPermission)
    {
        try
        {
            if(RebirthRemoteResourceCraftOriginal.HasRejection(original)||proof?.Length!=64||proof.Any(c=>!Uri.IsHexDigit(c))||authenticatedPermission?.Invoke()!=true||!RebirthRemoteResourceClientOriginal.Current(player,original)||
                player.Buffs.GetCustomVar(Receipt(original.Operation))!=1f||!RebirthRemoteResourceCraftOriginal.TryRead(original,out var action,out var state)||
                state!=RebirthCamperTransferJournal.Phase.Uncertain||(string)action.Attribute("source")!=source.ToString("N")||(string)action.Attribute("binding")!=binding||
                !holds.TryGetValue(original.Operation,out var hold)||!Current(hold)||hold.Refund||hold.SourceOperation!=source||hold.SourceBinding!=binding||
                RebirthLogisticsOwnerReceipt.IsHeld(player)||RebirthPlacedOriginal.IsHeld(player)||RebirthCookingOutputDelivery.HasOwnHold(player)||RebirthCookingOwnerDelta.HasOwnHold(player)||
                RebirthCookingGridCustody.IsHeld(player)||AdvancedFarmingHarvestOwnerDelta.IsHeld(player)||AdvancedFarmingWaterExchangeOriginal.IsHeld(player)||
                RebirthGearOwnerReservation.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player)||RebirthCamperEnergyOwnerHold.Blocks(player.bag)||RebirthCamperStationNativeActorPayment.Blocks(player.bag))return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor").Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value);
            if(!hold.Before.Matches(before)||!hold.After.Matches(after)||!CraftMixture(before,after,RebirthCamperEnergyActorImage.Capture(player)))return false;
            var rows=player.PlayerUI.xui.GetCraftingData()?.RecipeQueueItems?.Where(row=>row?.Recipe!=null&&IsMarked(row.Recipe,source,binding)).ToArray();
            if(rows?.Length!=1||rows[0].RepairItem!=null&&rows[0].RepairItem.type!=0||rows[0].AmountToRepair!=0||rows[0].Multiplier!=(int)action.Attribute("count")||rows[0].Quality!=(int)action.Attribute("quality")||
                !AcceptedCraftImage(original,action,source,binding,RecipeImage(rows[0].Recipe))||!RebirthRemoteResourceCraftRecipe.TryRead(action.Element("markedRecipe").Value,out var final))return false;
            // The narrow permission is scoped to this synchronous continuation,
            // never a callback replay or a permanent upgrade of mixed custody.
            bool blocking=hold.BlockingOnly;hold.BlockingOnly=false;
            try
            {
                foreach(var grid in new[]{hold.Bag,hold.Belt})
                {
                    var target=ReferenceEquals(grid,hold.Bag)?after.Bag:after.Belt;
                    for(int slot=0;slot<target.Length;slot++)if(!Same(grid.items[slot],target[slot])&&
                        (authenticatedPermission?.Invoke()!=true||!CraftMixture(before,after,RebirthCamperEnergyActorImage.Capture(player))||!WriteOriginalSlot(player,original.Operation,grid,slot,target[slot])))return false;
                }
                if(!after.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
                rows[0].Recipe.ingredients.Clear();rows[0].Recipe.ingredients.AddRange(final.ingredients.Select(stack=>stack.Clone()));
                if(RecipeImage(rows[0].Recipe)!=action.Element("markedRecipe").Value||!RebirthRemoteResourceCraftOriginal.ObserveLocalUsage(player,original))return false;
                player.Buffs.SetCustomVar(Receipt(original.Operation),2f,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
            }
            finally{hold.BlockingOnly=blocking;}
        }
        catch{return false;}
    }
}
