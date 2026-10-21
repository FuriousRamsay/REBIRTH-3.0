using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Placed original input only. No source admission, owner permission, tile
// setter, callback or publication is performed by these capture/read methods.
internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    internal static bool IsCandidate(RebirthRemoteResourceClientOriginal.Intent original)=>original?.Kind==RemoteResourceClientOperation.Craft&&
        original.Copy().Element("action")?.Attribute("placed")!=null;
    internal static bool Capture(EntityPlayerLocal player,ItemActionEntryCraft entry,ulong epoch,ulong request,List<ItemStack> requirements,List<ItemStack> remote,
        out RebirthRemoteResourceClientOriginal.Intent original)
    {
        original=null;
        try
        {
            var source=(entry?.ItemController as XUiC_RecipeEntry)?.Recipe;
            if(!RebirthRemoteResourcePlacedStationClientCustody.IsInstalled||!AllowsReviewedPlacedMode||source?.ingredients==null||source.ingredients.Count<1||source.IsScrap||RebirthCookingHeat.Managed(source)||RebirthRecyclerRuntime.IsMarked(source)||RebirthStationGridQueue.IsMarked(source)||
                !RebirthRemoteResourceOwnerAction.CanPrepareNativeOriginal(player)||
                !RebirthRemoteResourcePlacedStationScope.TryObserveWindow(player,entry,out var station,out var payment,out bool turnsOn)||
                !RebirthRemoteResourcePlacedStationScope.TryGetWindowName(player.world,station,out string window))return false;
            int count=entry.craftCountControl.Count,tier=entry.craftingTier;
            if(count<1||count>short.MaxValue||tier<0||tier>byte.MaxValue||requirements==null||remote==null||remote.Count<1||
                entry.tempIngredientList==null||entry.tempIngredientList.Count!=source.ingredients.Count)return false;
            var before=RebirthCamperEnergyActorImage.Capture(player);
            if(payment==RebirthRemoteResourcePlacedStationScope.PaymentSource.OwnerInventory)
            {if(!RebirthRemoteResourceCraftPlan.IsExactShortfall(before,requirements,remote,player.inventory.Length))return false;}
            else
            {
                var input=entry.craftCountControl.WindowGroup.Controller.GetChildByType<XUiC_WorkstationInputGrid>()?.GetSlots();
                if(input==null||input.Length!=station.Input?.Length||input.Where((item,index)=>!RebirthStationGridIngredients.IsSameStackSnapshot(item,station.Input[index])).Any()||
                    !RebirthRemoteResourcePlacedStationInputPlan.TryCreate(input,requirements,remote,out _))return false;
            }
            var template=new Recipe{itemValueType=source.itemValueType,count=XUiM_Recipes.GetRecipeCraftOutputCount(entry.ItemController.xui,source),
                craftingArea=source.craftingArea,craftExpGain=source.craftExpGain,craftingTime=XUiM_Recipes.GetRecipeCraftTime(entry.ItemController.xui,source),
                craftingToolType=source.craftingToolType,craftingTier=tier,tags=source.tags};
            template.AddIngredients(entry.tempIngredientList.Select(item=>item.Clone()).ToList());
            if(template.count<1||float.IsNaN(template.craftingTime)||float.IsInfinity(template.craftingTime)||template.craftingTime<0f)return false;
            if(!RebirthRecipeOutputs.TryCaptureFrozenOutputs(source,out string outputImage))return false;
            if(!TryUiQueue(entry.ItemController.GetParentByType<XUiC_WorkstationWindowGroup>(),out var uiQueue))return false;
            var position=station.ToWorldPos();
            var action=Action(position,station.blockValue.rawData,window,count,tier,player.inventory.Length,payment,turnsOn,source.materialBasedRecipe,
                source.craftingToolType,source.tags.ToString(),RebirthRemoteResourceOwnerAction.RecipeImage(source),RebirthRemoteResourceOwnerAction.RecipeImage(template),
                Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(station.Input)),RebirthRemoteResourcePlacedStationQueue.Encode(uiQueue),RebirthRecipeOutputs.DefinitionIdentity(source),outputImage);
            return RebirthRemoteResourceClientOriginal.TryPrepare(player,epoch,request,RemoteResourceClientOperation.Craft,requirements,remote,action,out original)&&
                ValidateInput(original,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _)&&
                RebirthRemoteResourcePlacedStationClientCustody.RememberActualCapture(entry,original);
        }
        catch{original=null;return false;}
    }
    private static XElement Action(Vector3i position,uint block,string window,int count,int tier,int belt,RebirthRemoteResourcePlacedStationScope.PaymentSource payment,
        bool turnsOn,bool material,int tool,string tags,string authored,string template,string input,string queue,string outputDefinition,string outputImage)=>
        new XElement("action",new XAttribute("placed",1),new XAttribute("x",position.x),new XAttribute("y",position.y),new XAttribute("z",position.z),
            new XAttribute("block",block),new XAttribute("window",window),new XAttribute("count",count),new XAttribute("tier",tier),new XAttribute("belt",belt),
            new XAttribute("payment",(byte)payment),new XAttribute("turnOn",turnsOn),new XAttribute("material",material),new XAttribute("tool",tool),new XAttribute("tags",tags),
            new XElement("authored",authored),new XElement("template",template),new XElement("input",input),new XElement("queue",queue),new XElement("outputDefinition",outputDefinition),new XElement("outputImage",outputImage));
    internal static bool ValidateInput(RebirthRemoteResourceClientOriginal.Intent original,out Vector3i position,out uint block,out string window,out Recipe authored,out Recipe template,
        out int count,out int tier,out int belt,out RebirthRemoteResourcePlacedStationScope.PaymentSource payment,out bool turnsOn,out ItemStack[] input)
    {
        position=default;block=0;window=null;authored=template=null;count=tier=belt=0;payment=default;turnsOn=false;input=null;
        try
        {
            if(!IsCandidate(original))return false;var action=original.Copy().Element("action");
            if(action.Attributes().Count()!=14||action.Elements().Count()!=6||(int?)action.Attribute("placed")!=1||
                !RebirthRemoteResourcePlacedStationAllocation.TryAdmitIntentBeforeHydration(original.Copy())||
                !RebirthRemoteResourceCraftRecipe.TryRead(action.Element("authored")?.Value,out authored)||
                !RebirthRemoteResourceCraftRecipe.TryRead(action.Element("template")?.Value,out template))return false;
            position=new Vector3i((int)action.Attribute("x"),(int)action.Attribute("y"),(int)action.Attribute("z"));block=(uint)action.Attribute("block");
            window=(string)action.Attribute("window");count=(int)action.Attribute("count");tier=(int)action.Attribute("tier");belt=(int)action.Attribute("belt");
            payment=(RebirthRemoteResourcePlacedStationScope.PaymentSource)checked((byte)(int)action.Attribute("payment"));turnsOn=(bool)action.Attribute("turnOn");
            bool material=(bool)action.Attribute("material");int tool=(int)action.Attribute("tool");string tags=(string)action.Attribute("tags");
            var definition=action.Element("outputDefinition");
            if(definition==null||definition.HasAttributes||definition.HasElements||definition.Value.Length<1||definition.Value.Length>RebirthRemoteResourcePlacedStationQueue.MaximumBytes)return false;
            var outputImage=action.Element("outputImage");
            if(outputImage==null||outputImage.HasAttributes||outputImage.HasElements||outputImage.Value.Length<1||outputImage.Value.Length>1048576)return false;
            if(window==null||window.Length<1||window.Length>512||count<1||count>short.MaxValue||tier<0||tier>byte.MaxValue||belt<0||belt>20||
                payment!=RebirthRemoteResourcePlacedStationScope.PaymentSource.OwnerInventory&&payment!=RebirthRemoteResourcePlacedStationScope.PaymentSource.StationInput||
                tags==null||tags.Length>4096||tool<0||template.count<1||float.IsNaN(template.craftingTime)||float.IsInfinity(template.craftingTime)||template.craftingTime<0f||
                authored.IsScrap||template.IsScrap||RebirthCookingHeat.Managed(authored)||RebirthCookingHeat.Managed(template)||
                RebirthRecyclerRuntime.IsMarked(authored)||RebirthRecyclerRuntime.IsMarked(template)||RebirthStationGridQueue.IsMarked(authored)||RebirthStationGridQueue.IsMarked(template)||
                authored.itemValueType!=template.itemValueType||authored.craftingArea!=template.craftingArea||authored.craftExpGain!=template.craftExpGain)return false;
            var required=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("requirements").Value));
            var totals=required.GroupBy(item=>item.itemValue.type).ToDictionary(group=>group.Key,group=>group.Sum(item=>(long)item.count));
            int originalCount=count;
            var nativeTotals=template.ingredients.GroupBy(item=>item.itemValue.type).ToDictionary(group=>group.Key,group=>group.Sum(item=>(long)item.count)*originalCount);
            if(totals.Count!=nativeTotals.Count||nativeTotals.Any(pair=>!totals.TryGetValue(pair.Key,out long total)||total!=pair.Value))return false;
            input=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(action.Element("input").Value));
            if(input.Length>byte.MaxValue||Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(input))!=action.Element("input").Value||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode(action.Element("queue").Value,out _))return false;
            authored.materialBasedRecipe=material;authored.craftingToolType=tool;authored.tags=FastTags<TagGroup.Global>.Parse(tags);
            template.craftingToolType=tool;template.craftingTier=tier;template.tags=authored.tags;
            var canonical=Action(position,block,window,count,tier,belt,payment,turnsOn,material,tool,tags,action.Element("authored").Value,action.Element("template").Value,action.Element("input").Value,action.Element("queue").Value,definition.Value,outputImage.Value);
            return XNode.DeepEquals(action,canonical);
        }
        catch{return false;}
    }
}
