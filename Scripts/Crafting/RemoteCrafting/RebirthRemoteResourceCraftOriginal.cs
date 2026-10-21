using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Globalization;

// Typed personal-craft intent/action journal. Transport and actual host native
// publication must still grant permission; this record alone grants nothing.
internal static class RebirthRemoteResourceCraftOriginal
{
    private static RebirthRemoteResourceClientOriginalJournal Actions()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteCraftOwnerOriginals"));
    private static RebirthRemoteResourceClientOriginalJournal Rejections()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteCraftOwnerRejections"));
    internal static bool HasRejection(RebirthRemoteResourceClientOriginal.Intent original)
    {try{return Rejections().Read(original.Operation)!=null;}catch{return true;}}
    private static bool Hex(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    private static bool Scalar(XElement value)=>value!=null&&!value.HasAttributes&&value.Nodes().All(node=>node.GetType()==typeof(XText));
    internal static bool IsPersonalEntry(ItemActionEntryCraft entry)
    {
        if(entry?.ItemController==null||entry.craftCountControl?.WindowGroup?.Controller==null||entry.craftCountControl.WindowGroup.Controller is XUiC_WorkstationWindowGroup||
            entry.ItemController.GetParentByType<XUiC_WorkstationWindowGroup>()!=null)return false;
        // Native OnActivated chooses the first visible crafting group, which
        // may differ from a stale action entry's own controller hierarchy.
        var selected=entry.ItemController.xui?.GetChildrenByType<XUiC_CraftingWindowGroup>()?.FirstOrDefault(group=>group?.WindowGroup?.isShowing==true);
        return selected!=null&&selected is not XUiC_WorkstationWindowGroup&&
            (selected.craftCountControl==null?entry.craftCountControl.Count==1:selected.craftCountControl.Count==entry.craftCountControl.Count);
    }
    internal static bool Capture(EntityPlayerLocal player,ItemActionEntryCraft entry,ulong epoch,ulong request,List<ItemStack> full,List<ItemStack> remote,
        out RebirthRemoteResourceClientOriginal.Intent original)
    {
        original=null;
        try
        {
            var source=(entry?.ItemController as XUiC_RecipeEntry)?.Recipe;
            if(source==null||!IsPersonalEntry(entry)||!RebirthRemoteResourceOwnerAction.CanPrepareNativeOriginal(player)||
                entry.craftCountControl.WindowGroup?.Controller.GetChildByType<XUiC_WorkstationInputGrid>()!=null||
                !RebirthRemoteResourceCraftPlan.TryCreate(player,entry,remote,out _,out _,out var template,out int count))return false;
            var action=new XElement("action",new XAttribute("recipe",source.GetName()),new XAttribute("count",count),new XAttribute("quality",entry.craftingTier),
                new XAttribute("qualityIngredients",source.ingredients.Any(stack=>stack.itemValue.HasQuality)?1:0),new XAttribute("beltCount",player.inventory.Length),
                new XElement("baseRecipe",RebirthRemoteResourceOwnerAction.RecipeImage(source)),new XElement("template",RebirthRemoteResourceOwnerAction.RecipeImage(template)));
            return RebirthRemoteResourceClientOriginal.TryPrepare(player,epoch,request,RemoteResourceClientOperation.Craft,full,remote,action,out original);
        }
        catch{original=null;return false;}
    }
    internal static bool ValidateInput(RebirthRemoteResourceClientOriginal.Intent original,out Recipe template,out int count,out int quality,out int beltCount,out bool qualityIngredients,bool requireCurrentRecipe=false)
    {
        template=null;count=quality=beltCount=0;qualityIngredients=false;
        try
        {
            var action=original?.Copy().Element("action");
            if(original?.Kind!=RemoteResourceClientOperation.Craft||action==null||action.Attributes().Count()!=5||action.Elements().Count()!=2||action.Nodes().Any(node=>node is not XElement)||
                action.Elements().Any(node=>node.Name!="baseRecipe"&&node.Name!="template")||
                !Scalar(action.Element("baseRecipe"))||!Scalar(action.Element("template"))||
                !RebirthRemoteResourceCraftRecipe.TryRead(action.Element("baseRecipe")?.Value,out var authored)||!RebirthRemoteResourceCraftRecipe.TryRead(action.Element("template")?.Value,out template))return false;
            count=(int)action.Attribute("count");quality=(int)action.Attribute("quality");beltCount=(int)action.Attribute("beltCount");int uses=(int)action.Attribute("qualityIngredients");
            if(count<1||count>short.MaxValue||quality<0||quality>byte.MaxValue||beltCount<0||beltCount>20||uses<0||uses>1||
                action.Attribute("count").Value!=count.ToString(CultureInfo.InvariantCulture)||action.Attribute("quality").Value!=quality.ToString(CultureInfo.InvariantCulture)||
                action.Attribute("beltCount").Value!=beltCount.ToString(CultureInfo.InvariantCulture)||action.Attribute("qualityIngredients").Value!=uses.ToString(CultureInfo.InvariantCulture)||
                Encoding.UTF8.GetByteCount((string)action.Attribute("recipe")??"")>512||
                authored.GetName()!=(string)action.Attribute("recipe")||template.itemValueType!=authored.itemValueType||template.IsScrap!=authored.IsScrap||template.craftingArea!=authored.craftingArea||
                (uses==1)!=authored.ingredients.Any(stack=>stack.itemValue.HasQuality))return false;
            // Optional fresh static catalogue qualification must never turn
            // recovery into replanning. Native owning-player modifiers and
            // the positively saved native queue keep native-owner trust.
            string image=action.Element("baseRecipe").Value;
            if(requireCurrentRecipe&&!CraftingManager.GetAllRecipes().Any(recipe=>recipe!=null&&recipe.GetName()==authored.GetName()&&RebirthRemoteResourceOwnerAction.RecipeImage(recipe)==image))return false;
            qualityIngredients=uses==1;template.craftingTier=quality;return true;
        }
        catch{template=null;return false;}
    }
    internal static bool Expected(RebirthRemoteResourceClientOriginal.Intent original,ItemStack[] paid,Guid source,string binding,
        out RebirthCamperEnergyActorImage before,out RebirthCamperEnergyActorImage after,out Recipe marked,out int count,out int quality)
    {
        before=after=null;marked=null;count=quality=0;
        try
        {
            if(!Hex(binding)||source==Guid.Empty||!ValidateInput(original,out var template,out count,out quality,out int belt,out bool hasQuality))return false;
            before=RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value);
            var needs=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("requirements").Value));
            if(!RebirthRemoteResourceCraftPlan.TryDebit(before,needs,paid,belt,out after,out var removed))return false;
            if(hasQuality){template.ingredients.Clear();template.AddIngredients(removed);}
            return RebirthRemoteResourceCraftRecipe.TryMarked(template,source,binding,out marked);
        }
        catch{before=after=null;marked=null;return false;}
    }
    internal static bool Prepare(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,string sourceWorld,Guid source,string binding,ItemStack[] paid,out XElement action)
    {
        action=null;
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||HasRejection(original)||!Hex(sourceWorld)||
                !Expected(original,paid,source,binding,out var before,out var after,out var marked,out int count,out int quality)||
                !before.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            var body=Body(original,sourceWorld,source,binding,before,after,marked,count,quality);
            byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(bytes.Length>24*1024*1024)return false;
            var record=Actions().Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
            return TryRead(original,out action,out _);
        }
        catch{action=null;return false;}
    }
    private static XElement Body(RebirthRemoteResourceClientOriginal.Intent original,string world,Guid source,string binding,RebirthCamperEnergyActorImage before,
        RebirthCamperEnergyActorImage after,Recipe marked,int count,int quality)=>new XElement("craftOwnerOriginal",new XAttribute("original",original.Digest),new XAttribute("sourceWorld",world),
        new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),new XAttribute("count",count),new XAttribute("quality",quality),
        new XElement("beforeActor",RebirthRemoteResourceClientOriginal.ActorImage(before)),new XElement("afterActor",RebirthRemoteResourceClientOriginal.ActorImage(after)),new XElement("markedRecipe",RebirthRemoteResourceOwnerAction.RecipeImage(marked)));
    internal static bool Canonical(RebirthRemoteResourceClientOriginal.Intent original,string world,Guid source,string binding,ItemStack[] paid,out XElement action)
    {
        action=null;
        try
        {
            if(!Hex(world)||!Expected(original,paid,source,binding,out var before,out var after,out var marked,out int count,out int quality))return false;
            action=Body(original,world,source,binding,before,after,marked,count,quality);
            return Encoding.UTF8.GetByteCount(action.ToString(SaveOptions.DisableFormatting))<=24*1024*1024;
        }
        catch{action=null;return false;}
    }
    internal static bool TryRead(RebirthRemoteResourceClientOriginal.Intent original,out XElement action,out RebirthCamperTransferJournal.Phase phase)
    {
        action=null;phase=RebirthCamperTransferJournal.Phase.Prepared;
        try
        {
            var record=Actions().Read(original.Operation);
            if(record==null||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||!record.Before.SequenceEqual(record.After)||record.Before.Length>24*1024*1024)return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),24*1024*1024);
            if(body.Name!="craftOwnerOriginal"||body.Attributes().Count()!=6||body.Elements().Count()!=3||body.Nodes().Any(node=>node is not XElement)||
                !Scalar(body.Element("beforeActor"))||!Scalar(body.Element("afterActor"))||!Scalar(body.Element("markedRecipe"))||(string)body.Attribute("original")!=original.Digest||
                !Hex((string)body.Attribute("sourceWorld"))||!Hex((string)body.Attribute("binding"))||!Guid.TryParseExact((string)body.Attribute("source"),"N",out var source)||source==Guid.Empty||
                !RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out var world,out var saved,out var binding,out var paid)||world!=(string)body.Attribute("sourceWorld")||saved!=source||binding!=(string)body.Attribute("binding")||
                !Expected(original,paid,source,binding,out var before,out var after,out var marked,out int count,out int quality)||
                (string)body.Attribute("count")!=count.ToString(CultureInfo.InvariantCulture)||(string)body.Attribute("quality")!=quality.ToString(CultureInfo.InvariantCulture)||body.Element("beforeActor")?.Value!=RebirthRemoteResourceClientOriginal.ActorImage(before)||
                body.Element("afterActor")?.Value!=RebirthRemoteResourceClientOriginal.ActorImage(after)||body.Element("markedRecipe")?.Value!=RebirthRemoteResourceOwnerAction.RecipeImage(marked))return false;
            phase=record.State;action=body;return true;
        }
        catch{return false;}
    }
    internal static bool BeginExecution(RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(HasRejection(original)||!TryRead(original,out _,out var phase)||phase!=RebirthCamperTransferJournal.Phase.Prepared)return false;
        return Actions().Advance(original.Operation,phase,RebirthCamperTransferJournal.Phase.Uncertain).State==RebirthCamperTransferJournal.Phase.Uncertain;
    }
    internal static bool SaveRejection(RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            var retained=Actions().Read(original.Operation);string digest="none";
            if(retained!=null){if(!TryRead(original,out var action,out _))return false;digest=RebirthRemoteResourceDurableJournal.Digest(action.ToString(SaveOptions.DisableFormatting));}
            byte[] bytes=Encoding.UTF8.GetBytes(new XElement("craftReject",new XAttribute("original",original.Digest),new XAttribute("action",digest)).ToString(SaveOptions.DisableFormatting));
            var journal=Rejections();var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied;
        }
        catch{return false;}
    }
    internal static bool SaveOwnerFive(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||!HasRejection(original)||
                !RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value).Matches(RebirthCamperEnergyActorImage.Capture(player))||
                RebirthRemoteResourceClientOriginal.QueueImage(player.PlayerUI.xui.GetCraftingData())!=original.Copy().Element("queueBefore").Value)return false;
            player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation),5f,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
        }
        catch{return false;}
    }
    internal static bool Complete(RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(!TryRead(original,out _,out var phase))return false;
        if(phase==RebirthCamperTransferJournal.Phase.Prepared)phase=Actions().Advance(original.Operation,phase,RebirthCamperTransferJournal.Phase.Uncertain).State;
        if(phase==RebirthCamperTransferJournal.Phase.Uncertain)phase=Actions().Advance(original.Operation,phase,RebirthCamperTransferJournal.Phase.Applied).State;
        return phase==RebirthCamperTransferJournal.Phase.Applied;
    }
    internal static bool CompleteIfPresent(RebirthRemoteResourceClientOriginal.Intent original)
    {try{return Actions().Read(original.Operation)==null||Complete(original);}catch{return false;}}
    internal static bool ObserveLocalUsage(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||!TryRead(original,out var action,out var state)||state==RebirthCamperTransferJournal.Phase.Prepared||
                !Guid.TryParseExact((string)action.Attribute("source"),"N",out var source)||
                !RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value).Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            var rows=player.PlayerUI.xui.GetCraftingData()?.RecipeQueueItems?.Where(row=>row?.Recipe!=null&&RebirthRemoteResourceOwnerAction.IsMarked(row.Recipe,source,(string)action.Attribute("binding"))).ToArray();
            return rows?.Length==1&&(rows[0].RepairItem==null||rows[0].RepairItem.type==0)&&rows[0].AmountToRepair==0&&rows[0].Multiplier==(int)action.Attribute("count")&&rows[0].Quality==(int)action.Attribute("quality")&&
                RebirthRemoteResourceOwnerAction.RecipeImage(rows[0].Recipe)==action.Element("markedRecipe").Value;
        }
        catch{return false;}
    }
}
