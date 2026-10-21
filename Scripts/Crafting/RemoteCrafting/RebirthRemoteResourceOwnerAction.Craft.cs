using System;
using System.Linq;
using HarmonyLib;

// Narrow original native callback permit; not installed/admitted by itself.
internal static partial class RebirthRemoteResourceOwnerAction
{
    internal static bool HasBlockingOnlyCraft(EntityPlayerLocal player)=>player!=null&&holds.Values.Any(hold=>ReferenceEquals(hold.Player,player)&&hold.BlockingOnly&&
        ReferenceEquals(hold.World,GameManager.Instance?.World)&&hold.Directory==GameIO.GetSaveGameDir()&&hold.Original.Deaths==player.Died&&
        hold.Original.Creation==RebirthCamperCharacterAccess.CurrentCreation(player)&&IsHeld(player));
    internal static bool TryHoldMixedCraft(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Func<bool> originalScope)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||originalScope?.Invoke()!=true||
                !RebirthRemoteResourceCraftOriginal.TryRead(original,out var action,out var state)||state!=RebirthCamperTransferJournal.Phase.Uncertain||
                !Guid.TryParseExact((string)action.Attribute("source"),"N",out var source)||holds.ContainsKey(original.Operation)||!CanPrepareNativeOriginal(player))return false;
            float phase=player.Buffs.GetCustomVar(Receipt(original.Operation));if(phase!=1f&&phase!=2f&&phase!=5f)return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor").Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value);
            var current=RebirthCamperEnergyActorImage.Capture(player);
            if(!DebitOnly(before.Bag,after.Bag)||!DebitOnly(before.Belt,after.Belt)||!Same(before.Cursor,after.Cursor)||!Same(current.Cursor,before.Cursor)||
                before.BagLocks==null||after.BagLocks==null||current.BagLocks==null||!before.BagLocks.SequenceEqual(after.BagLocks)||!before.BagLocks.SequenceEqual(current.BagLocks))return false;
            foreach(var area in new[]{new[]{before.Bag,after.Bag,current.Bag},new[]{before.Belt,after.Belt,current.Belt}})
            {
                if(area[0].Length!=area[1].Length||area[0].Length!=area[2].Length)return false;
                for(int slot=0;slot<area[0].Length;slot++)if(!Same(area[2][slot],area[0][slot])&&!Same(area[2][slot],area[1][slot]))return false;
            }
            // Retained Uncertain custody only. This is deliberately not an
            // effect receipt, callback capability or restoration permit.
            holds.Add(original.Operation,new Hold{Player=player,World=player.world,Directory=GameIO.GetSaveGameDir(),Original=original,Before=before,After=after,
                Bag=player.bag.ItemGrid,Belt=player.inventory.ItemGrid,Authorized=originalScope,SourceOperation=source,SourceBinding=(string)action.Attribute("binding"),BlockingOnly=true});return true;
        }
        catch{return false;}
    }
    internal static bool TryHoldRestoredCraft(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Func<bool> authenticatedOriginal)
    {
        try
        {
            if(!RebirthRemoteResourceCraftOriginal.ObserveLocalUsage(player,original)||authenticatedOriginal?.Invoke()!=true||
                !RebirthRemoteResourceCraftOriginal.TryRead(original,out var action,out _)||!Guid.TryParseExact((string)action.Attribute("source"),"N",out var source))return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor").Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value);string binding=(string)action.Attribute("binding");
            if(holds.TryGetValue(original.Operation,out var retained))
            {
                if(!ReferenceEquals(retained.Player,player)||retained.Original.Digest!=original.Digest||!retained.Before.Matches(before)||
                    retained.SourceOperation!=Guid.Empty&&(retained.SourceOperation!=source||retained.SourceBinding!=binding))return false;
                retained.After=after.Copy();retained.SourceOperation=source;retained.SourceBinding=binding;retained.Authorized=authenticatedOriginal;return Current(retained);
            }
            if(!CanPrepareNativeOriginal(player))return false;
            holds.Add(original.Operation,new Hold{Player=player,World=player.world,Directory=GameIO.GetSaveGameDir(),Original=original,Before=before,After=after,
                Bag=player.bag.ItemGrid,Belt=player.inventory.ItemGrid,Authorized=authenticatedOriginal,SourceOperation=source,SourceBinding=binding});return true;
        }
        catch{return false;}
    }
    private sealed class CraftScope:IDisposable
    {
        internal EntityPlayerLocal Player;internal ItemActionEntryCraft Entry;
        internal RebirthRemoteResourceClientOriginal.Intent Original;internal Func<bool> Permission;internal bool Entered;
        internal Guid Source;internal string Binding;internal Recipe CapturedRecipe;
        public void Dispose()
        {
            if(!ReferenceEquals(craftScope,this))return;
            try{SealCraftRecipe(CapturedRecipe);}finally{craftScope=null;}
        }
    }
    [ThreadStatic]private static CraftScope craftScope;
    internal static bool CaptureCraftRecipe(XUiC_CraftingWindowGroup group,Recipe recipe)
    {
        var scope=craftScope;if(scope==null)return true;
        try
        {
            if(!scope.Entered||scope.Permission?.Invoke()!=true||group is XUiC_WorkstationWindowGroup||!ReferenceEquals(group?.xui,scope.Player.PlayerUI.xui)||
                !RebirthRemoteResourceCraftOriginal.IsPersonalEntry(scope.Entry)||!holds.TryGetValue(scope.Original.Operation,out var hold)||hold.BlockingOnly||!Current(hold))return false;
            if(scope.CapturedRecipe!=null)return ReferenceEquals(scope.CapturedRecipe,recipe)&&StampActualRecipe(recipe,scope.Source,scope.Binding);
            if(RecipeImage(recipe)!=scope.Original.Copy().Element("action").Element("template").Value)return false;
            if(!StampActualRecipe(recipe,scope.Source,scope.Binding))return false;
            scope.CapturedRecipe=recipe;return true;
        }
        catch{return false;}
    }
    internal static void SealCraftRecipe(Recipe recipe)
    {
        var scope=craftScope;if(scope==null||recipe==null||!ReferenceEquals(recipe,scope.CapturedRecipe)||!scope.Entered)return;
        // Correlation only: native queue acceptance can precede local payment.
        // Retag the same native recipe after its quality ingredients rebuild,
        // including exceptional exits; payment still needs saved owner2+After.
        // Admission already authenticated this exact newly created native
        // recipe reference. Losing later permission must not remove its fence.
        // This marks correlation only and never permits a setter or payment.
        try{StampActualRecipe(recipe,scope.Source,scope.Binding);}catch{}
    }
    private static bool EnterCraftOriginal(BaseItemActionEntry entry)
    {
        var scope=craftScope;
        if(scope==null||scope.Entered||!ReferenceEquals(scope.Entry,entry)||scope.Permission?.Invoke()!=true||
            !holds.TryGetValue(scope.Original.Operation,out var hold)||hold.BlockingOnly||!Current(hold)||!hold.Before.Matches(RebirthCamperEnergyActorImage.Capture(scope.Player)))return false;
        scope.Entered=true;return true;
    }
    private static bool ConsumeCraftOriginal(ItemStackGrid grid,int slot,ItemStack value)
    {
        var scope=craftScope;
        if(scope?.Entered!=true||scope.Permission?.Invoke()!=true||!holds.TryGetValue(scope.Original.Operation,out var hold)||hold.BlockingOnly||!Current(hold))return false;
        bool bag=ReferenceEquals(grid,hold.Bag);if(!bag&&!ReferenceEquals(grid,hold.Belt))return false;
        var before=bag?hold.Before.Bag:hold.Before.Belt;var after=bag?hold.After.Bag:hold.After.Belt;
        return slot>=0&&slot<after.Length&&value!=null&&Same(value,after[slot])&&(Same(grid.items[slot],before[slot])||Same(grid.items[slot],after[slot]));
    }
    internal static IDisposable BeginCraftOriginalScope(EntityPlayerLocal player,ItemActionEntryCraft entry,RebirthRemoteResourceClientOriginal.Intent original,
        Guid source,string binding,Func<bool> actualSavedHostPermission)
    {
        if(craftScope!=null||repairScope!=null||original?.Kind!=RemoteResourceClientOperation.Craft||!RebirthRemoteResourceClientOriginal.Current(player,original)||
            !ReferenceEquals(entry?.ItemController?.xui?.playerUI?.entityPlayer,player)||actualSavedHostPermission?.Invoke()!=true||
            !holds.TryGetValue(original.Operation,out var hold)||hold.BlockingOnly||!Current(hold)||hold.SourceOperation!=source||hold.SourceBinding!=binding||
            !hold.Before.Matches(RebirthCamperEnergyActorImage.Capture(player)))return null;
        var scope=new CraftScope{Player=player,Entry=entry,Original=original,Permission=actualSavedHostPermission,Source=source,Binding=binding};craftScope=scope;return scope;
    }
}

[HarmonyPatch(typeof(XUiC_CraftingWindowGroup),nameof(XUiC_CraftingWindowGroup.AddItemToQueue),new[]{typeof(Recipe)})]
internal static class RebirthRemoteResourceCraftOriginalQueueCapture
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_CraftingWindowGroup __instance,Recipe _recipe,ref bool __result)
    {if(RebirthRemoteResourceOwnerAction.CaptureCraftRecipe(__instance,_recipe))return true;__result=false;return false;}
}
[HarmonyPatch(typeof(Recipe),nameof(Recipe.AddIngredients),new[]{typeof(System.Collections.Generic.List<ItemStack>)})]
internal static class RebirthRemoteResourceCraftOriginalIngredientsSeal
{private static void Postfix(Recipe __instance)=>RebirthRemoteResourceOwnerAction.SealCraftRecipe(__instance);}
