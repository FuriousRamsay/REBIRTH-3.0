using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Purpose-specific immutable owner intent, retained before source admission.
// No native payment, queue setters or original owner callback run here.
internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    private static RebirthRemoteResourceClientOriginalJournal Admissions()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","FetchedStationSourceAdmissions"));
    internal static bool IsCandidate(RebirthRemoteResourceClientOriginal.Intent original)=>original?.Kind==RemoteResourceClientOperation.Craft&&original.Copy().Element("action")?.Attribute("station")!=null;
    private static bool Scope(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)=>ThreadManager.IsMainThread()&&actor?.world?.IsRemote()==false&&!RebirthSurvivorMode.IsEnabledForCurrentWorld()&&
        actor.IsSpawned()&&!actor.IsDead()&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
        original?.Kind==RemoteResourceClientOperation.Craft&&actor.Died==original.Deaths&&actor.world.worldState.Guid==original.World&&
        RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.CanonicalId==original.Owner&&RebirthOriginalActionScope.Matches(actor,owner.StorageKey,original.Creation);
    internal static bool Capture(EntityPlayerLocal player,ItemActionEntryCraft entry,ulong epoch,ulong request,List<ItemStack> full,List<ItemStack> remote,out RebirthRemoteResourceClientOriginal.Intent original)
    {
        original=null;
        try
        {
            var recipe=(entry?.ItemController as XUiC_RecipeEntry)?.Recipe;
            var window=entry?.ItemController?.GetParentByType<XUiC_WorkstationWindowGroup>();
            if(recipe==null||recipe.IsScrap||recipe.materialBasedRecipe||RebirthCookingHeat.Managed(recipe)||RebirthRecyclerRuntime.IsMarked(recipe)||entry.craftCountControl==null||window==null||window.inputWindow!=null||RebirthSurvivorMode.IsEnabledForCurrentWorld()||
                !ReferenceEquals(entry.ItemController.xui?.playerUI?.entityPlayer,player)||!RebirthRemoteResourceOwnerAction.CanPrepareNativeOriginal(player)||
                !RebirthCamperStationWindowSession.TryGetCurrent(player,out var station,out var address)||!ReferenceEquals(window.WorkstationData?.TileEntity,station)||
                !RebirthStationPlayerIngredients.UsesPlayerPayment(player,station)||!RebirthRemoteResourceCraftPlan.IsExactShortfall(RebirthCamperEnergyActorImage.Capture(player),full,remote,player.inventory.Length))return false;
            int count=entry.craftCountControl.Count,tier=entry.craftingTier;if(count<1||count>9999||tier<0||tier>6)return false;
            var action=new XElement("action",new XAttribute("station",address.ToString()),new XAttribute("block",station.InstalledBlock.rawData),new XAttribute("recipe",recipe.GetName()),
                new XAttribute("count",count),new XAttribute("tier",tier),new XAttribute("belt",player.inventory.Length),new XAttribute("tool",recipe.craftingToolType),
                new XElement("baseRecipe",RebirthRemoteResourceOwnerAction.RecipeImage(recipe)));
            return RebirthRemoteResourceClientOriginal.TryPrepare(player,epoch,request,RemoteResourceClientOperation.Craft,full,remote,action,out original)&&
                ValidateInput(original,false,out _,out _,out _,out _,out _,out _,out _);
        }
        catch{original=null;return false;}
    }
    private static bool ValidateInput(RebirthRemoteResourceClientOriginal.Intent original,bool currentCatalogue,out RebirthCamperStationAddress address,out Recipe recipe,
        out int count,out int tier,out int belt,out int tool,out uint block)
    {
        address=default;recipe=null;count=tier=belt=tool=0;block=0;
        try
        {
            if(!IsCandidate(original))return false;var action=original.Copy().Element("action");
            if(action.Attributes().Count()!=7||action.Elements().Count()!=1||action.Element("baseRecipe")==null||action.Nodes().Any(node=>node is not XElement)||
                action.Element("baseRecipe").HasAttributes||action.Element("baseRecipe").Nodes().Any(node=>node is not XText)||
                !RebirthCamperStationAddress.TryParse((string)action.Attribute("station"),out address)||!RebirthRemoteResourceCraftRecipe.TryRead(action.Element("baseRecipe").Value,out var parsed))return false;
            count=(int)action.Attribute("count");tier=(int)action.Attribute("tier");belt=(int)action.Attribute("belt");tool=(int)action.Attribute("tool");block=(uint)action.Attribute("block");
            string name=(string)action.Attribute("recipe");
            if(count<1||count>9999||tier<0||tier>6||belt<0||belt>20||tool<0||tool!=0&&ItemClass.GetForId(tool)==null||name==null||Encoding.UTF8.GetByteCount(name)>512||
                parsed.GetName()!=name||parsed.IsScrap||parsed.materialBasedRecipe)return false;
            var canonical=new XElement("action",new XAttribute("station",address.ToString()),new XAttribute("block",block),new XAttribute("recipe",name),new XAttribute("count",count),
                new XAttribute("tier",tier),new XAttribute("belt",belt),new XAttribute("tool",tool),new XElement("baseRecipe",action.Element("baseRecipe").Value));
            if(!XNode.DeepEquals(action,canonical))return false;
            int originalTool=tool;
            recipe=currentCatalogue?CraftingManager.GetAllRecipes().FirstOrDefault(candidate=>candidate!=null&&candidate.GetName()==name&&candidate.craftingToolType==originalTool&&
                RebirthRemoteResourceOwnerAction.RecipeImage(candidate)==action.Element("baseRecipe").Value):parsed;
            if(recipe==null)return false;return true;
        }
        catch{return false;}
    }
    private static bool Publish(RebirthRemoteResourceClientOriginal.Intent original,Guid source,byte[] bytes)
    {
        var journal=Admissions();var record=journal.Prepare(source,original.Operation,original.Creation,0,bytes,bytes);
        if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(source,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(source,record.State,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    internal static bool Admit(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!Scope(actor,original)||!ValidateInput(original,false,out var address,out _,out int count,out int tier,out int belt,out int tool,out uint block)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out var world))return false;
            Guid source=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
            if(RebirthRemoteResourcePreAdmissionRefusal.BlocksAdmission(source))return false;
            byte[] bytes=Encoding.UTF8.GetBytes(original.Copy().ToString(SaveOptions.DisableFormatting));var retained=Admissions().Read(source);
            if(retained!=null)return retained.Vehicle==original.Operation&&retained.OwnerCreation==original.Creation&&retained.Revision==0&&retained.Before.SequenceEqual(bytes)&&retained.After.SequenceEqual(bytes)&&Publish(original,source,bytes);
            if(!ValidateInput(original,true,out _,out var recipe,out _,out _,out _,out _,out _)||!RebirthStationLiveAccess.TryGetMobileLease(actor,address,original.Creation,out var station,out _)||
                station.InstalledBlock.rawData!=block||!RebirthCamperStationNativeCrafting.AllowedFetchedOriginal(actor,station,recipe,tier)||
                !(actor is EntityPlayerLocal?RebirthStationPlayerIngredients.UsesPlayerPayment(actor,station):RebirthCamperRemoteStationPlayerSource.IsCurrent(actor,station))||
                RebirthCamperModuleRuntime.For(station.BoundVehicle).TransferPending||RebirthCamperStationNativeActorPayment.IsHeld(station)||RebirthCamperStationGridCustody.IsHeld(station)||RebirthRemoteResourceSourceCustody.BlocksStation(station)||
                !RebirthCamperEnergySavedOwner.TryRead(owner,out var before,out var saved)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||belt!=actor.inventory.Length||
                !RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value).Matches(before)||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=10f)return false;
            var full=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("requirements").Value));
            var remote=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("remoteRequirements").Value));
            var needs=new List<ItemStack>();
            foreach(var ingredient in recipe.ingredients)
            {if(!RebirthCraftingIngredientQuantity.TryResolveTotal(actor,recipe,ingredient,tier,count,out int total)||total<1)return false;needs.Add(new ItemStack(ingredient.itemValue.Clone(),total));}
            var actual=full.GroupBy(stack=>stack.itemValue.type).ToDictionary(group=>group.Key,group=>group.Sum(stack=>(long)stack.count));
            var expected=needs.GroupBy(stack=>stack.itemValue.type).ToDictionary(group=>group.Key,group=>group.Sum(stack=>(long)stack.count));
            if(actual.Count!=expected.Count||expected.Any(pair=>!actual.TryGetValue(pair.Key,out long amount)||amount!=pair.Value)||
                !RebirthRemoteResourceCraftPlan.IsExactShortfall(before,needs,remote,belt)||!RebirthRemoteResourceCraftPlan.TryDebit(before,needs,remote,belt,out _,out _))return false;
            return Publish(original,source,bytes);
        }
        catch{return false;}
    }
    internal static bool IsMappedRoot(RebirthRemoteResourceDurableJournal.Record root)
    {
        if(root==null)return false;
        try
        {
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","FetchedStationSourceAdmissions");if(!Directory.Exists(directory))return false;
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return true;
            return Directory.EnumerateFileSystemEntries(directory,root.Operation.ToString("N")+"*").Any()||Directory.Exists(Path.Combine(directory,"completed",root.Operation.ToString("N")));
        }
        catch{return true;}
    }
    internal static bool TryReadAdmission(RebirthRemoteResourceDurableJournal.Record root,out RebirthRemoteResourceClientOriginal.Intent original)
    {
        original=null;
        try
        {
            var record=Admissions().Read(root.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Revision!=0||record.OwnerCreation!=root.Creation||record.Before.Length>16*1024*1024||!record.Before.SequenceEqual(record.After)||
                !RebirthRemoteResourceClientOriginal.TryDecodeOriginal(new UTF8Encoding(false,true).GetString(record.Before),out var found)||record.Vehicle!=found.Operation||
                !ValidateInput(found,false,out _,out _,out _,out _,out _,out _,out _)||root.Creation!=found.Creation||(int)root.Copy().Attribute("deaths")!=found.Deaths||
                RebirthStablePlayerIdentity.ComputeStorageKey(found.Owner)!=root.Owner||root.Operation!=RebirthRemoteResourceDurableJournal.Operation(root.World,root.Owner,found.Creation,found.Epoch,found.Request,found.Kind))return false;
            original=found;return true;
        }
        catch{return false;}
    }
}
