using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;

// Native RecipeQueueItem is the sole persistent queue. A shared token connects
// output rows to the ONE original payment; it is never a second inventory.
public static class RebirthRecyclerRuntime
{
    internal const string Prefix="rebirth.recycler.";
    private sealed class Group
    {
        internal Guid Id; internal ItemValue Original; internal int Units,Outputs;
        internal string Owner,Creation,Rule; internal int Index;
    }
    private sealed class Processing {internal bool Active;internal int NativeOwnerFrame=-1;}
    private static readonly ConditionalWeakTable<TileEntityWorkstation,Processing> processing=new ConditionalWeakTable<TileEntityWorkstation,Processing>();
    private static bool installed;
    public static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.recycler.native.groups");
        foreach(var type in new[]{typeof(RebirthRecyclerQueueProcess),typeof(RebirthRecyclerQueueCancel),typeof(RebirthRecyclerQueueProjectionOnly),typeof(RebirthRecyclerQueueWriteback)})
            RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
        ModEvents.GameStartDone.RegisterHandler(WorldReady);installed=true;
    }
    private static void WorldReady(ref ModEvents.SGameStartDoneData data)=>RebirthRecyclerRules.Reload();
    internal static bool IsProcessing(TileEntityWorkstation station)=>station!=null&&processing.TryGetValue(station,out var state)&&state.Active;
    internal static bool HasGroups(RecipeQueueItem[] queue)=>queue!=null&&queue.Any(row=>IsMarked(row?.Recipe));
    internal static bool IsMarked(Recipe recipe)=>recipe?.ingredients?.Count>0&&recipe.ingredients[0]?.itemValue?.HasMetadata(Prefix+"group")==true;
    internal static bool TryGroup(Recipe recipe,out Guid group)
    {group=Guid.Empty;return IsMarked(recipe)&&recipe.ingredients[0].itemValue.TryGetMetadata(Prefix+"group",out string id)&&Guid.TryParseExact(id,"N",out group)&&group!=Guid.Empty;}
    public static bool Supports(TileEntityWorkstation station)=>station?.block!=null&&RebirthRecyclerRules.Supports(station.block.GetBlockName());
    private static string Identity(EntityPlayer player)
    {
        var data=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        return data?.PrimaryId?.CombinedString??(player is EntityPlayerLocal?GameManager.Instance.getPersistentPlayerID(null)?.CombinedString:null);
    }
    private static bool Access(EntityPlayer actor,TileEntityWorkstation station,Func<bool> current)
    {
        if(!ThreadManager.IsMainThread()||actor==null||actor.IsDead()||actor.world==null||!ReferenceEquals(GameManager.Instance?.World,actor.world)
            ||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||!Supports(station)||current?.Invoke()!=true)return false;
        if(actor.world.IsRemote())
        {
            if(!(actor is EntityPlayerLocal local))return false;
            if(station is RebirthCamperStationState remoteMobile)
                return RebirthCamperModuleInteraction.Current(remoteMobile.BoundVehicle,local)
                    &&RebirthCamperStationWindowSession.TryGetCurrent(local,out var presented,out _)&&ReferenceEquals(presented,remoteMobile);
            return LockManager.Instance?.IsLockedByLocalPlayer(station,0)==true;
        }
        if(station is RebirthCamperStationState mobile)
        {
            var address=new RebirthCamperStationAddress(RebirthCamperModuleRuntime.For(mobile.BoundVehicle).Identity,mobile.InstallationId);
            return !RebirthCamperStationGridCustody.IsHeld(mobile)&&!RebirthCamperModuleRuntime.For(mobile.BoundVehicle).TransferPending
                &&RebirthStationLiveAccess.TryGetMobileLease(actor,address,RebirthCamperCharacterAccess.CurrentCreation(actor),out var live,out _)
                &&ReferenceEquals(live,mobile);
        }
        if(!ReferenceEquals(actor.world.GetTileEntity(station.ToWorldPos()),station)||(actor.position-station.ToWorldPos().ToVector3()).sqrMagnitude>64f)return false;
        var locks=LockManager.Instance;
        if(locks==null||station.IsSharedLock(0)||!locks.singleLocks.TryGetByKey(actor.entityId,out var entries)||entries==null
            ||!entries.Any(entry=>ReferenceEquals(entry.Target,station)&&entry.Channel==0))return false;
        var identity=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId)?.PrimaryId;
        if(identity==null&&actor is EntityPlayerLocal)identity=GameManager.Instance.GetPersistentLocalPlayer()?.PrimaryId;
        return identity!=null&&RebirthWorkstationSecurityService.CanAccessWorkstation(actor.world,station.ToWorldPos(),actor,identity,false,
            RebirthSecureAccessPurpose.WorkstationAccess,out _);
    }
    public static bool CanQueue(EntityPlayer actor,TileEntityWorkstation station,int inputSlot,int units,out string reason)
    {
        reason="xuiCannotBeScrappedHere";
        if(actor!=null&&(RebirthCamperStationNativeActorPayment.Blocks(actor.bag)||RebirthCamperStationNativeActorPayment.Blocks(actor.inventory))
            ||station is RebirthCamperStationState busy&&RebirthCamperStationGridCustody.IsHeld(busy))
        {reason="xuiRebirthCamperStationSaving";return false;}
        if(!Supports(station)||station.Input==null||inputSlot<0||inputSlot>=Math.Min(3,station.Input.Length)||units<1||units>ushort.MaxValue)return false;
        var source=station.Input[inputSlot];
        if(source==null||source.IsEmpty()||source.count<units||source.itemValue.Metadata?.Count>byte.MaxValue-10
            ||source.itemValue.Metadata?.Keys.Any(k=>k.StartsWith(Prefix,StringComparison.Ordinal))==true)return false;
        if(!RebirthRecyclerRules.TryResolve(source.itemValue.type,station.block.GetBlockName(),out var rule,out reason))return false;
        if(!RebirthRecyclerRules.ToolsHealthy(station.Tools,rule,out var missing,out var broken))
        {reason=missing.Count>0?"xuiScrapToolRequired":"xuiScrapToolBroken";return false;}
        if(RebirthRecyclerBonuses.UnavailableLegacyFeatures(source.itemValue).Any(issue=>issue!="legacy-armor-mastery-progression"))
        {reason="xuiRebirthRecyclingRuleUnavailable";return false;}
        if(station.Queue==null||station.Queue.Count(row=>row==null||row.Recipe==null||row.Multiplier<=0)<rule.Outputs.Count)
        {reason="xuiRebirthRecyclingQueueFull";return false;}
        foreach(var output in rule.Outputs)
        {
            long count=(long)output.Stack.count*units;float seconds=output.TimeSeconds*units;
            if(count<1||count>int.MaxValue||float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0)
            {reason="xuiRebirthRecyclingBatchTooLarge";return false;}
        }
        reason=null;return true;
    }
    public static bool CanAccept(EntityPlayer actor,TileEntityWorkstation station,ItemStack source,out string reason)
    {
        reason="xuiCannotBeScrappedHere";
        if(!Supports(station)||source==null||source.IsEmpty()||source.count<1||source.count>ushort.MaxValue
            ||source.itemValue.Metadata?.Count>byte.MaxValue-10||source.itemValue.Metadata?.Keys.Any(k=>k.StartsWith(Prefix,StringComparison.Ordinal))==true)return false;
        if(!RebirthRecyclerRules.TryResolve(source.itemValue.type,station.block.GetBlockName(),out var rule,out reason))return false;
        if(!RebirthRecyclerRules.ToolsHealthy(station.Tools,rule,out var missing,out _))
        {reason=missing.Count>0?"xuiScrapToolRequired":"xuiScrapToolBroken";return false;}
        if(RebirthRecyclerBonuses.UnavailableLegacyFeatures(source.itemValue).Any(issue=>issue!="legacy-armor-mastery-progression"))
        {reason="xuiRebirthRecyclingRuleUnavailable";return false;}
        if(station.Queue==null||station.Queue.Count(row=>row==null||row.Recipe==null&&row.RepairItem==null||row.Multiplier<=0)<rule.Outputs.Count)
        {reason="xuiRebirthRecyclingQueueFull";return false;}
        foreach(var output in rule.Outputs)
            if((long)output.Stack.count*source.count>int.MaxValue||float.IsInfinity(output.TimeSeconds*source.count))
            {reason="xuiRebirthRecyclingBatchTooLarge";return false;}
        reason=null;return true;
    }
    public static bool TryQueue(EntityPlayer actor,TileEntityWorkstation station,int inputSlot,int units,Guid operation,Func<bool> current,out string reason)
    {
        reason="xuiCannotBeScrappedHere";
        if(operation==Guid.Empty||!Access(actor,station,current))return false;
        if(actor.world.IsRemote()&&!RebirthRecyclerTransport.IsNativeOwner(station,actor))return RebirthRecyclerTransport.Request(actor as EntityPlayerLocal,station,inputSlot,units,operation,false,out reason);
        if(station is RebirthCamperStationState held&&RebirthCamperStationNativeActorPayment.IsHeld(held))
            return RebirthCamperStationNativeActorPayment.MatchesPendingOperation(held,operation)
                &&RebirthCamperStationNativeActorPayment.TryResume(actor,held,new RebirthCamperStationAddress(RebirthCamperModuleRuntime.For(held.BoundVehicle).Identity,held.InstallationId),RebirthCamperCharacterAccess.CurrentCreation(actor));
        if(station.Queue.Any(row=>TryGroup(row?.Recipe,out var id)&&id==operation))return true; // settled replay
        if(!CanQueue(actor,station,inputSlot,units,out reason))return false;
        var source=station.Input[inputSlot].Clone();
        if(!RebirthRecyclerRules.TryResolve(source.itemValue.type,station.block.GetBlockName(),out var rule,out reason))return false;
        var original=source.itemValue.Clone();var attachments=Detach(original);
        string owner=Identity(actor),creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
        if(string.IsNullOrEmpty(owner)||string.IsNullOrEmpty(creation)){reason="xuiRebirthRecyclingAccessExpired";return false;}
        var input=ItemStack.Clone(station.Input);input[inputSlot].count-=units;if(input[inputSlot].count==0)input[inputSlot]=ItemStack.Empty.Clone();
        var queue=(RecipeQueueItem[])station.Queue.Clone();
        // Append behind existing active rows. Output order matches legacy input-rule order.
        var vacant=Enumerable.Range(0,queue.Length).Reverse().Where(i=>queue[i]==null||queue[i].Recipe==null||queue[i].Multiplier<=0).ToArray();
        for(int outputIndex=0;outputIndex<rule.Outputs.Count;outputIndex++)
        {
            var output=rule.Outputs[outputIndex];var stack=output.Stack;float time=output.TimeSeconds*units;
            var token=original.Clone();
            token.SetMetadata(Prefix+"group",operation.ToString("N"));token.SetMetadata(Prefix+"source",RebirthNativeItemCodec.Encode(original));
            token.SetMetadata(Prefix+"units",units);token.SetMetadata(Prefix+"outputs",rule.Outputs.Count);token.SetMetadata(Prefix+"index",outputIndex);
            token.SetMetadata(Prefix+"owner",owner);token.SetMetadata(Prefix+"creation",creation);token.SetMetadata(Prefix+"rule",rule.SourceId);
            var recipe=new Recipe {itemValueType=stack.itemValue.type,count=checked(stack.count*units),IsScrap=true,IsTrackable=false,
                craftingTime=time,craftExpGain=0,UseIngredientModifier=false,ingredients=new List<ItemStack>{new ItemStack(token,1)}};
            int slot=vacant[outputIndex];queue[slot]=new RecipeQueueItem {Recipe=recipe,Multiplier=1,CraftingTimeLeft=time,OneItemCraftTime=time,StartingEntityId=actor.entityId,IsCrafting=slot==queue.Length-1};
        }
        if(!Access(actor,station,current)||!RebirthStationGridIngredients.IsSameStackSnapshot(station.Input[inputSlot],source))return false;
        if(station is RebirthCamperStationState mobile)
        {
            // The existing coupled actor/vehicle journal handles attachment return and cancellation.
            return RebirthRecyclerOwnerDelivery.Begin(actor,mobile,operation,input,queue,attachments,out reason);
        }
        // Placed stations retain their native chunk/lock identity and native player destination.
        if(!RebirthRecyclerOwnerDelivery.TryDeliverPlaced(actor,station,attachments,out reason))return false;
        station.Input=input;station.Queue=queue;station.ResetTickTime();station.setModified();Refresh(actor,station);return true;
    }
    private static List<ItemStack> Detach(ItemValue value)
    {
        var result=new List<ItemStack>();
        foreach(var cells in new[]{value.modifications,value.cosmeticMods})
            if(cells!=null)for(int i=0;i<cells.Length;i++)if(cells[i]!=null&&!cells[i].IsEmpty())
            {result.Add(new ItemStack(cells[i].Clone(),1));cells[i]=ItemValue.None;}
        return result;
    }
    private static bool Read(Recipe recipe,out Group group)
    {
        group=null;if(!TryGroup(recipe,out var id))return false;var value=recipe.ingredients[0].itemValue;
        if(!recipe.IsScrap||recipe.ingredients.Count!=1||recipe.ingredients[0].count!=1||recipe.craftExpGain!=0
            ||!value.TryGetMetadata(Prefix+"source",out string image)||!RebirthNativeItemCodec.TryDecode(image,out var original)
            ||original==null||original.IsEmpty()||original.type!=value.type
            ||!value.TryGetMetadata(Prefix+"units",out int units)||units<1||units>ushort.MaxValue
            ||!value.TryGetMetadata(Prefix+"outputs",out int outputs)||outputs<1||outputs>254
            ||!value.TryGetMetadata(Prefix+"index",out int index)||index<0||index>=outputs
            ||!value.TryGetMetadata(Prefix+"owner",out string owner)||string.IsNullOrEmpty(owner)
            ||!value.TryGetMetadata(Prefix+"creation",out string creation)||string.IsNullOrEmpty(creation)
            ||!value.TryGetMetadata(Prefix+"rule",out string rule)||rule?.Length!=64)return false;
        group=new Group {Id=id,Original=original,Units=units,Outputs=outputs,Index=index,Owner=owner,Creation=creation,Rule=rule};return true;
    }
    internal static bool ValidRow(TileEntityWorkstation station,RecipeQueueItem row)
    {
        if(!Read(row?.Recipe,out var group)||row.Multiplier!=1||row.RepairItem!=null||row.StartingEntityId<0
            ||!RebirthRecyclerRules.TryResolve(group.Original.type,station.block.GetBlockName(),out var rule)||rule.SourceId!=group.Rule
            ||rule.Outputs.Count!=group.Outputs)return false;
        var output=rule.Outputs[group.Index];
        return row.Recipe.itemValueType==output.Stack.itemValue.type&&row.Recipe.count==(long)output.Stack.count*group.Units
            &&Math.Abs(row.OneItemCraftTime-output.TimeSeconds*group.Units)<.001f
            &&row.CraftingTimeLeft>=-.001f&&row.CraftingTimeLeft<=row.OneItemCraftTime+.001f;
    }
    public static bool TryCancel(EntityPlayer actor,TileEntityWorkstation station,Guid groupId,Func<bool> current,out string reason)
    {
        reason="xuiRebirthRecyclingAccessExpired";
        if(groupId==Guid.Empty||!Access(actor,station,current))return false;
        if(actor.world.IsRemote()&&!RebirthRecyclerTransport.IsNativeOwner(station,actor))return RebirthRecyclerTransport.Request(actor as EntityPlayerLocal,station,0,0,groupId,true,out reason);
        var rows=station.Queue.Select((row,index)=>new{row,index}).Where(x=>TryGroup(x.row?.Recipe,out var id)&&id==groupId).ToArray();
        if(rows.Length==0)return false;
        if(!Read(rows[0].row.Recipe,out var group))return false;
        if(station is RebirthCamperStationState&&(group.Owner!=Identity(actor)||group.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)))return false;
        if(rows.Length!=group.Outputs){reason="xuiScrapPartiallyCompleted";return false;}
        var indices=new HashSet<int>();
        foreach(var entry in rows)if(!ValidRow(station,entry.row)||!Read(entry.row.Recipe,out var sibling)||!indices.Add(sibling.Index)
            ||sibling.Rule!=group.Rule||sibling.Units!=group.Units||sibling.Owner!=group.Owner||sibling.Creation!=group.Creation
            ||RebirthNativeItemCodec.Encode(sibling.Original)!=RebirthNativeItemCodec.Encode(group.Original))return false;
        var queue=(RecipeQueueItem[])station.Queue.Clone();foreach(var entry in rows)queue[entry.index]=new RecipeQueueItem {StartingEntityId=-1};
        var refunds=new List<ItemStack>{new ItemStack(group.Original.Clone(),group.Units)};
        if(station is RebirthCamperStationState mobile)
            return RebirthRecyclerOwnerDelivery.Begin(actor,mobile,CancelOperation(groupId),ItemStack.Clone(station.Input),queue,refunds,out reason,groupId);
        if(!RebirthRecyclerOwnerDelivery.TryDeliverPlaced(actor,station,refunds,out reason))return false;
        station.Queue=queue;station.ResetTickTime();station.setModified();Refresh(actor,station);return true;
    }
    internal static Guid CancelOperation(Guid group)
    {using(var hash=SHA256.Create()){var bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(group.ToString("N")+":recycler-cancel"));var id=new byte[16];Array.Copy(bytes,id,16);return new Guid(id);}}
    // Scoped actor payment can change several rows only when all are one exact recycler group.
    internal static bool ValidDelta(RecipeQueueItem[] before,RecipeQueueItem[] after,Guid group)
    {
        if(group==Guid.Empty||before==null||after==null||before.Length!=after.Length)return false;
        bool insertion=false,removal=false;int changed=0;
        for(int i=0;i<before.Length;i++)
        {
            if(ReferenceEquals(before[i],after[i]))continue;changed++;
            bool was=TryGroup(before[i]?.Recipe,out var a)&&a==group,now=TryGroup(after[i]?.Recipe,out var b)&&b==group;
            if(was&&(after[i]?.Recipe==null||after[i].Multiplier<=0))removal=true;
            else if(now&&(before[i]?.Recipe==null||before[i].Multiplier<=0))insertion=true;
            else return false;
        }
        return changed>0&&insertion!=removal;
    }
    internal static bool PreservesGroups(RecipeQueueItem[] before,RecipeQueueItem[] after)
    {
        try
        {
        if(before==null||after==null)return !HasGroups(before)&&!HasGroups(after);
        var originals=before.Where(row=>IsMarked(row?.Recipe)).ToArray();var incoming=after.Where(row=>IsMarked(row?.Recipe)).ToArray();
        if(originals.Length!=incoming.Length)return false;
        var taken=new bool[incoming.Length];
        foreach(var row in originals)
        {
            byte[] expected=RowImage(row);bool found=false;
            for(int i=0;i<incoming.Length;i++)if(!taken[i]&&expected.SequenceEqual(RowImage(incoming[i])))
            {taken[i]=true;found=true;break;}
            if(!found)return false;
        }
        return true;
        }
        catch {return false;}
    }
    private static byte[] RowImage(RecipeQueueItem row)
    {
        // IsCrafting is presentation-only for these server-owned grouped timers.
        var clone=new RecipeQueueItem {Recipe=row.Recipe,Multiplier=row.Multiplier,CraftingTimeLeft=row.CraftingTimeLeft,
            OneItemCraftTime=row.OneItemCraftTime,StartingEntityId=row.StartingEntityId,Quality=row.Quality,
            RepairItem=row.RepairItem,AmountToRepair=row.AmountToRepair,IsCrafting=false};
        using(var stream=new System.IO.MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(stream);clone.Write(writer);writer.Flush();return stream.ToArray();}
    }
    internal static bool Process(TileEntityWorkstation station,float elapsed)
    {
        var row=station?.Queue?.LastOrDefault();
        if(!IsMarked(row?.Recipe))return true;
        var world=GameManager.Instance?.World;
        // Native remote windows only project this queue; the authoritative tile/mobile clock owns it.
        var nativeActor=world?.GetPrimaryPlayer();bool nativeOwner=world?.IsRemote()==true&&RebirthRecyclerTransport.IsNativeOwner(station,nativeActor);
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()&&!nativeOwner||!Supports(station)||!ValidRow(station,row)
            ||float.IsNaN(elapsed)||float.IsInfinity(elapsed)||elapsed<=0)return false;
        var nativeLocks=LockManager.Instance;
        if(!world.IsRemote()&&!(station is RebirthCamperStationState)&&station.bUserAccessing&&nativeLocks!=null
            &&nativeLocks.singleLocks.TryGetByValue(new LockManager.LockEntry(station,0),out var nativeUser)
            &&!(world.GetEntity(nativeUser) is EntityPlayerLocal))return false;
        if(station is RebirthCamperStationState mobile&&(!ReferenceEquals(mobile.BoundVehicle?.world,world)
            ||RebirthCamperStationGridCustody.IsHeld(mobile)||RebirthCamperModuleRuntime.For(mobile.BoundVehicle).TransferPending))return false;
        if(station.isModuleUsed[(int)TileEntityWorkstation.Module.Fuel]&&!station.IsBurning)return false;
        if(!Read(row.Recipe,out var group)||!RebirthRecyclerRules.TryResolve(group.Original.type,station.block.GetBlockName(),out var rule)
            ||row.CraftingTimeLeft>0&&!RebirthRecyclerRules.ToolsHealthy(station.Tools,rule,out _,out _))return false;
        float rate=RebirthRecyclerBonuses.WearRatePerSecond(station.block);
        float seconds=Math.Min(Math.Max(0,row.CraftingTimeLeft),elapsed);
        seconds=Math.Min(seconds,RebirthRecyclerBonuses.MaxProcessingSeconds(station.Tools,rule.RequiredTools,rate));
        if(seconds<=0&&row.CraftingTimeLeft>0)return false;
        var state=processing.GetValue(station,_=>new Processing());if(state.Active)return false;
        if(nativeOwner){if(state.NativeOwnerFrame==UnityEngine.Time.frameCount)return false;state.NativeOwnerFrame=UnityEngine.Time.frameCount;}
        state.Active=true;
        try
        {
            var next=new RecipeQueueItem {Recipe=row.Recipe,Multiplier=row.Multiplier,Quality=row.Quality,StartingEntityId=row.StartingEntityId,
                IsCrafting=true,OneItemCraftTime=row.OneItemCraftTime,CraftingTimeLeft=Math.Max(0,row.CraftingTimeLeft-seconds)};
            var queue=(RecipeQueueItem[])station.Queue.Clone();queue[queue.Length-1]=next;
            if(next.CraftingTimeLeft<=.0001f)
            {
                var output=ItemStack.Clone(station.Output);
                if(ItemStack.AddToItemStackArray(output,new ItemStack(new ItemValue(row.Recipe.itemValueType,false),row.Recipe.count))==-1)
                {
                    // Time/tool work can finish while output space is unavailable.
                    // Keep the paid native row at zero; never rerun its work/wear.
                    station.Queue=queue;station.Tools=RebirthRecyclerBonuses.ApplyWear(station.Tools,rule.RequiredTools,rate,seconds);
                    station.setModified();if(station is RebirthCamperStationState blocked)RebirthCamperModuleRuntime.For(blocked.BoundVehicle).Persist(true);
                    return false;
                }
                queue[queue.Length-1]=new RecipeQueueItem {StartingEntityId=-1};
                bool final=!queue.Any(sibling=>TryGroup(sibling?.Recipe,out var id)&&id==group.Id);
                var bonuses=final?RebirthRecyclerBonuses.CompletionRewards(group.Original,group.Id):new List<ItemStack>();
                if(bonuses.Count>0)
                {
                    var actor=world.Players?.list?.FirstOrDefault(candidate=>Identity(candidate)==group.Owner
                        &&RebirthCamperCharacterAccess.CurrentCreation(candidate)==group.Creation);
                    if(actor==null)return false;
                    if(station is RebirthCamperStationState bonusStation)
                    {
                        RebirthRecyclerOwnerDelivery.Begin(actor,bonusStation,CompletionOperation(group.Id),ItemStack.Clone(station.Input),queue,
                            bonuses,out _,group.Id,output,RebirthRecyclerBonuses.ApplyWear(station.Tools,rule.RequiredTools,rate,seconds));
                        return false; // Never let native processing run after a deferred grouped payment.
                    }
                    if(!RebirthRecyclerOwnerDelivery.TryDeliverPlaced(actor,station,bonuses,out _))return false;
                }
                station.Output=output;
                // Use native queue ordering/cycling, with one consumed output row.
                station.Queue=queue;station.cycleRecipeQueue();
            }
            else station.Queue=queue;
            station.Tools=RebirthRecyclerBonuses.ApplyWear(station.Tools,rule.RequiredTools,rate,seconds);
            station.setModified();
            if(station is RebirthCamperStationState owner)RebirthCamperModuleRuntime.For(owner.BoundVehicle).Persist(true);
            var local=world.GetPrimaryPlayer();if(local!=null)Refresh(local,station);
            return false;
        }
        finally {state.Active=false;}
    }
    private static Guid CompletionOperation(Guid group)
    {using(var hash=SHA256.Create()){var bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(group.ToString("N")+":recycler-complete"));var id=new byte[16];Array.Copy(bytes,id,16);return new Guid(id);}}
    internal static void Refresh(EntityPlayer actor,TileEntityWorkstation station)
    {
        if(!(actor is EntityPlayerLocal local))return;
        var ui=LocalPlayerUI.GetUIForPlayer(local);if(ui==null)return;
        foreach(var window in ui.xui.GetChildrenByType<XUiC_WorkstationWindowGroup>())
            if(window.windowGroup?.isShowing==true&&ReferenceEquals(window.WorkstationData?.TileEntity,station))
            {
                if(station is RebirthCamperStationState mobile)
                {if(RebirthCamperStationWindowSession.Admitted(window,mobile))RebirthCamperStationQueueProjection.Refresh(window,mobile);}
                else AccessTools.Method(typeof(XUiC_WorkstationWindowGroup),"syncUIfromTE")?.Invoke(window,null);
                window.SetAllChildrenDirty();
            }
    }
}

[HarmonyPatch(typeof(TileEntityWorkstation),"HandleRecipeQueue")]
internal static class RebirthRecyclerQueueProcess
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(TileEntityWorkstation __instance,float __0)=>RebirthRecyclerRuntime.Process(__instance,__0);
}
[HarmonyPatch(typeof(XUiC_RecipeStack),"HandleOnPress")]
internal static class RebirthRecyclerQueueCancel
{
    [HarmonyPriority(1000)]
    private static bool Prefix(XUiC_RecipeStack __instance)
    {
        if(!RebirthRecyclerRuntime.IsMarked(__instance.GetRecipe()))return true;
        var window=__instance.GetParentByType<XUiC_WorkstationWindowGroup>();var actor=__instance.xui?.playerUI?.entityPlayer;
        if(window?.WorkstationData?.TileEntity is TileEntityWorkstation station&&RebirthRecyclerRuntime.TryGroup(__instance.GetRecipe(),out var group))
        {
            bool current()=>window.WindowGroup?.isShowing==true&&ReferenceEquals(window.WorkstationData?.TileEntity,station);
            if(!RebirthRecyclerRuntime.TryCancel(actor,station,group,current,out var reason)&&actor!=null&&reason!=null)
                GameManager.ShowTooltip(actor,Localization.Get(reason));
        }
        return false;
    }
}
[HarmonyPatch(typeof(XUiC_RecipeStack),"Update")]
internal static class RebirthRecyclerQueueProjectionOnly
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_RecipeStack __instance,float __0)
    {
        if(!RebirthRecyclerRuntime.IsMarked(__instance.GetRecipe()))return true;
        var actor=__instance.xui?.playerUI?.entityPlayer;
        var station=__instance.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity;
        if(actor?.world?.IsRemote()==true&&!(station is RebirthCamperStationState))
            RebirthRecyclerTransport.TryAdvanceOwned(actor,station,__0);
        __instance.IsCrafting=false;__instance.IsDirty=true;__instance.updateRecipeData();return false;
    }
}
[HarmonyPatch(typeof(XUiM_Workstation),nameof(XUiM_Workstation.SetRecipeQueueItems))]
internal static class RebirthRecyclerQueueWriteback
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiM_Workstation __instance,RecipeQueueItem[] __0)
        =>RebirthRecyclerRuntime.IsProcessing(__instance?.TileEntity)
            ||RebirthRecyclerRuntime.PreservesGroups(__instance?.TileEntity?.Queue,__0);
}
