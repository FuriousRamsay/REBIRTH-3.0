using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;

#nullable disable

// An original shovel action owns this capsule. The native attack still charges
// its original wear/stamina once; only its positive harvest publication is held.
internal static class AdvancedFarmingHarvestTerminalPlot
{
    internal sealed class Capsule
    {
        internal Guid Source;internal uint Raw;internal int Damage;internal bool HarvestTool,BypassMaxDamage;
        internal bool? UseInventory;internal string CropOutputs,PlotOutputs,Tool;internal float DropX,DropY,DropZ;
    }
    private sealed class Pending
    {
        internal World World;internal EntityPlayerLocal Owner;internal Vector3i Plot,Crop;
        internal BlockValue PlotValue,CropValue;internal ItemActionAttack.AttackHitInfo Hit;
        internal Capsule Capsule;internal List<ItemStack> CropOutputs,PlotOutputs=new List<ItemStack>();
        internal ItemActionData Action;internal bool Harvesting,Failed;internal int Frame;
        internal bool PriorKilled;internal int PriorGiven,PriorMax,SyntheticGiven,SyntheticMax;internal float PriorTotal,SyntheticTotal;
    }
    private static readonly Dictionary<ItemActionAttack.AttackHitInfo,Pending> PendingActions=new Dictionary<ItemActionAttack.AttackHitInfo,Pending>();
    [ThreadStatic] private static Pending capturing;
    [ThreadStatic] private static ItemActionAttack.AttackHitInfo nativeHit;
    [ThreadStatic] internal static bool Committing;
    [ThreadStatic] private static World committingWorld;
    [ThreadStatic] private static Vector3i committingPosition;
    [ThreadStatic] private static uint committingRaw;
    internal static object EnterNativeHit(ItemActionAttack.AttackHitInfo hit)
    {var previous=nativeHit;nativeHit=hit;return previous;}
    internal static void LeaveNativeHit(object previous)=>nativeHit=previous as ItemActionAttack.AttackHitInfo;
    internal static bool SuppressHeldDamage(EntityAlive owner,MinEventTypes kind,bool useInventory=true)
    {
        if(kind!=MinEventTypes.onSelfDamagedBlock||nativeHit==null||!PendingActions.TryGetValue(nativeHit,out var p))return false;
        bool held=ReferenceEquals(p.Owner,owner)&&ReferenceEquals(p.World,GameManager.Instance?.World)&&!p.Owner.IsDead()&&!p.Failed&&p.Frame==Time.frameCount&&p.PlotValue.rawData==owner.MinEventContext.BlockValue.rawData&&p.Plot==nativeHit.hitRef.BlockPosition;
        if(held)p.Capsule.UseInventory=useInventory;return held;
    }
    internal static bool AllowsCommit(WorldBase world,Vector3i position,BlockValue value)=>Committing&&ReferenceEquals(world,committingWorld)&&position==committingPosition&&value.rawData==committingRaw;
    private static bool Supported(BlockValue plot)
    {
        var block=plot.Block;if(block is not BlockFarmPlotRebirth||!block.DowngradeBlock.isair||block.Stage2Health>0||block.itemsToDrop==null)return false;
        return block.itemsToDrop.TryGetValue(EnumDropEvent.Destroy,out var drops)&&drops.Count==1&&drops[0].name=="farmPlotBlockVariantHelper"&&(!block.itemsToDrop.TryGetValue(EnumDropEvent.Harvest,out var harvest)||harvest.Count==0);
    }
    internal static string Encode(Capsule c)=>new XElement("terminalPlot",new XAttribute("source",c.Source.ToString("N")),new XAttribute("raw",c.Raw),new XAttribute("damage",c.Damage),new XAttribute("harvestTool",c.HarvestTool),new XAttribute("bypass",c.BypassMaxDamage),new XAttribute("dropX",c.DropX),new XAttribute("dropY",c.DropY),new XAttribute("dropZ",c.DropZ),c.UseInventory.HasValue?new XAttribute("useInventory",c.UseInventory.Value):null,new XElement("crop",c.CropOutputs),new XElement("plot",c.PlotOutputs),new XElement("tool",c.Tool)).ToString(SaveOptions.DisableFormatting);
    internal static Capsule Decode(string image)
    {
        if(string.IsNullOrEmpty(image))return null;
        // The enclosing receipt already applies strict DTD/size/canonical XML.
        var e=AdvancedFarmingHarvestProtocol.ParseTerminalPayload(image);
        var c=new Capsule{UseInventory=(bool?)e.Attribute("useInventory"),Source=Guid.ParseExact((string)e.Attribute("source"),"N"),Raw=(uint)e.Attribute("raw"),Damage=(int)e.Attribute("damage"),HarvestTool=(bool)e.Attribute("harvestTool"),BypassMaxDamage=(bool)e.Attribute("bypass"),DropX=(float)e.Attribute("dropX"),DropY=(float)e.Attribute("dropY"),DropZ=(float)e.Attribute("dropZ"),CropOutputs=(string)e.Element("crop"),PlotOutputs=(string)e.Element("plot"),Tool=(string)e.Element("tool")};
        var plot=new BlockValue{rawData=c.Raw};var crop=AdvancedFarmingHarvestReceipt.ReadOutputs(c.CropOutputs);var outputs=AdvancedFarmingHarvestReceipt.ReadOutputs(c.PlotOutputs);var tool=AdvancedFarmingHarvestReceipt.ReadOutputs(c.Tool);
        if(float.IsNaN(c.DropX)||float.IsInfinity(c.DropX)||float.IsNaN(c.DropY)||float.IsInfinity(c.DropY)||float.IsNaN(c.DropZ)||float.IsInfinity(c.DropZ)||c.Source==Guid.Empty||!Supported(plot)||c.Damage<=0||plot.damage>=plot.Block.MaxDamage||(long)plot.damage+c.Damage<plot.Block.MaxDamage||crop.Length>1||outputs.Length>1||tool.Length!=1||tool[0].count!=1||Encode(c)!=image)throw new System.IO.InvalidDataException();
        return c;
    }
    internal static bool Stage(WorldBase world,Vector3i plot,BlockValue value,int damage,EntityPlayerLocal owner,ItemActionAttack.AttackHitInfo hit,bool harvestTool,bool bypassMaxDamage)
    {try{return StageCore(world,plot,value,damage,owner,hit,harvestTool,bypassMaxDamage);}catch(Exception error){Log.Warning("[REBIRTH Farming] terminal action refused: "+error.GetType().Name);return false;}}
    private static bool StageCore(WorldBase world,Vector3i plot,BlockValue value,int damage,EntityPlayerLocal owner,ItemActionAttack.AttackHitInfo hit,bool harvestTool,bool bypassMaxDamage)
    {
        foreach(var old in PendingActions.Where(pair=>!ReferenceEquals(pair.Value.World,GameManager.Instance?.World)||UnityEngine.Time.frameCount-pair.Value.Frame>2).ToArray())PendingActions.Remove(old.Key);
        if(world is not World concrete||!Supported(value)||hit==null||hit.hitRef.Type!=BlockValueRefType.Block||hit.hitRef.BlockPosition!=plot||!AdvancedFarmingHarvestProtocol.CompatibleSourceRaw(hit.blockBeingDamaged.rawData,value.rawData)||PendingActions.ContainsKey(hit)||PendingActions.Count>=4)return false;
        var crop=plot+Vector3i.up;var cropValue=world.GetBlock(crop);var drop=owner.GetDropPosition();
        if(!AdvancedFarmingHarvestProtocol.CanCaptureOriginal(world,crop,cropValue,owner)||!AdvancedFarmingHarvestProtocol.CanCaptureOriginal(world,plot,value,owner)||!AdvancedFarmingHarvestProtocol.TrySource(plot,value,out var source)||!RebirthUtilities.TryBuildRebirthCropHarvest(world,cropValue,owner,out var outputs))return false;
        PendingActions.Add(hit,new Pending{World=concrete,Owner=owner,Plot=plot,Crop=crop,PlotValue=value,CropValue=cropValue,Hit=hit,Frame=UnityEngine.Time.frameCount,CropOutputs=outputs,Capsule=new Capsule{Source=source,Raw=value.rawData,Damage=damage,DropX=drop.x,DropY=drop.y,DropZ=drop.z,HarvestTool=harvestTool,BypassMaxDamage=bypassMaxDamage,CropOutputs=AdvancedFarmingHarvestReceipt.OutputImage(outputs.ToArray()),PlotOutputs=AdvancedFarmingHarvestReceipt.OutputImage(Array.Empty<ItemStack>()),Tool=AdvancedFarmingHarvestReceipt.OutputImage(new[]{new ItemStack(owner.inventory.holdingItemItemValue.Clone(),1)})}});
        return true;
    }
    internal static object Open(ItemActionData action)
    {
        if(action?.attackDetails==null||!PendingActions.TryGetValue(action.attackDetails,out var p))return null;
        if(capturing!=null||!ReferenceEquals(action.invData?.holdingEntity,p.Owner)||!ReferenceEquals(GameManager.Instance?.World,p.World)||p.Owner.IsDead()||p.World.GetBlock(p.Plot).rawData!=p.PlotValue.rawData||p.World.GetBlock(p.Crop).rawData!=p.CropValue.rawData){p.Failed=true;return p;}
        p.Action=action;p.Harvesting=true;capturing=p;
        p.PriorKilled=action.attackDetails.bKilled;p.PriorGiven=action.attackDetails.damageGiven;p.PriorMax=action.attackDetails.damageMax;p.PriorTotal=action.attackDetails.damageTotalOfTarget;
        p.SyntheticGiven=p.PlotValue.Block.MaxDamage-p.PlotValue.damage;p.SyntheticMax=p.PlotValue.Block.MaxDamage;p.SyntheticTotal=p.PlotValue.Block.MaxDamage;
        // Hit was held physically, so expose the accepted terminal result only
        // inside this original native harvest call. Never replay the melee action.
        action.attackDetails.bKilled=true;action.attackDetails.damageGiven=p.SyntheticGiven;action.attackDetails.damageMax=p.SyntheticMax;action.attackDetails.damageTotalOfTarget=p.SyntheticTotal;
        return p;
    }
    internal static bool Collect(ItemActionData action,ItemValue item,int count,float probability,bool scale,out bool accepted)
    {
        accepted=false;var p=capturing;if(p==null||!ReferenceEquals(p.Action,action))return false;
        try
        {
            if(scale)
            {
                float per=(float)action.attackDetails.damageMax/count;
                int before=(int)((Utils.FastMin(action.attackDetails.damageTotalOfTarget,action.attackDetails.damageMax)-action.attackDetails.damageGiven)/per+0.5f);
                int after=Mathf.Min((int)(action.attackDetails.damageTotalOfTarget/per+0.5f),count);int original=count;count=after-before;
                if(action.attackDetails.damageTotalOfTarget>action.attackDetails.damageMax)count=Mathf.Min(count,original);
            }
            if(GameUtils.random==null){GameUtils.random=GameRandomManager.Instance.CreateGameRandom();GameUtils.random.SetSeed((int)System.Diagnostics.Stopwatch.GetTimestamp());}
            if(GameUtils.random.RandomFloat<=probability&&count>0)
            {
                if(p.PlotOutputs.Count>=1||count>ushort.MaxValue)throw new System.IO.InvalidDataException();
                p.PlotOutputs.Add(new ItemStack(item.Clone(),count));var position=p.Owner.GetDropPosition();p.Capsule.DropX=position.x;p.Capsule.DropY=position.y;p.Capsule.DropZ=position.z;accepted=true;
            }
        }
        catch(Exception error){p.Failed=true;Log.Warning("[REBIRTH Farming] terminal original harvest retained: "+error.GetType().Name);}
        return true;
    }
    internal static void Close(object state,Exception error)
    {
        if(state is not Pending p)return;
        if(ReferenceEquals(capturing,p))capturing=null;
        PendingActions.Remove(p.Hit);
        RestoreSynthesized(p);
        if(error!=null||p.Failed||!p.Harvesting)return;
        p.Capsule.PlotOutputs=AdvancedFarmingHarvestReceipt.OutputImage(p.PlotOutputs.ToArray());
        var outputs=new List<ItemStack>(p.CropOutputs);outputs.AddRange(p.PlotOutputs);
        try{AdvancedFarmingHarvestProtocol.Begin(p.World,p.Crop,p.CropValue,p.Owner,outputs,false,Encode(p.Capsule));}
        catch(Exception failure){Log.Warning("[REBIRTH Farming] original terminal capsule refused without source mutation: "+failure.GetType().Name);}
    }
    private static void RestoreSynthesized(Pending p)
    {
        if(!p.Harvesting||!ReferenceEquals(p.Action?.attackDetails,p.Hit))return;
        // Do not restore blockBeingDamaged: native HarvestOnAttack deliberately
        // clears it to Air. Keep any native/other observer change to our fields.
        if(p.Hit.bKilled)p.Hit.bKilled=p.PriorKilled;
        if(p.Hit.damageGiven==p.SyntheticGiven)p.Hit.damageGiven=p.PriorGiven;
        if(p.Hit.damageMax==p.SyntheticMax)p.Hit.damageMax=p.PriorMax;
        if(p.Hit.damageTotalOfTarget==p.SyntheticTotal)p.Hit.damageTotalOfTarget=p.PriorTotal;
    }
    internal static bool ValidYield(World world,EntityPlayer owner,AdvancedFarmingHarvestReceipt.Record r,ItemStack[] outputs)
    {
        var c=Decode(r.Terminal);if(c==null)return AdvancedFarmingHarvestAdmission.ValidOriginalYield(world,owner,new BlockValue{rawData=r.Raw},outputs,r.Replant);
        var crop=AdvancedFarmingHarvestReceipt.ReadOutputs(c.CropOutputs);var plot=AdvancedFarmingHarvestReceipt.ReadOutputs(c.PlotOutputs);
        var name=AdvancedFarmingHarvestReceipt.ReadOutputs(c.Tool)[0].itemValue.ItemClass?.Name;
        if(string.IsNullOrEmpty(name)||name.IndexOf("shovel",StringComparison.OrdinalIgnoreCase)<0&&(name.IndexOf("Admin",StringComparison.OrdinalIgnoreCase)<0||name.IndexOf("Tool",StringComparison.OrdinalIgnoreCase)<0))return false;
        if(Math.Abs(c.DropX-r.X)>8f||Math.Abs(c.DropY-r.Y)>8f||Math.Abs(c.DropZ-r.Z)>8f||r.Replant||!AdvancedFarmingHarvestAdmission.ValidOriginalYield(world,owner,new BlockValue{rawData=r.Raw},crop,false)||AdvancedFarmingHarvestReceipt.OutputImage(crop.Concat(plot).ToArray())!=r.Outputs)return false;
        var value=new BlockValue{rawData=c.Raw};
        // This exact authored physical family has one Destroy output and no
        // per-hit Harvest output. Native owner passive/RNG count is preserved.
        if(!value.Block.itemsToDrop.TryGetValue(EnumDropEvent.Destroy,out var drops)||drops.Count!=1||drops[0].name!="farmPlotBlockVariantHelper"||value.Block.itemsToDrop.TryGetValue(EnumDropEvent.Harvest,out var harvest)&&harvest.Count!=0)return false;
        return plot.Length==0||plot[0].count<=ushort.MaxValue&&RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(new ItemValue(ItemClass.GetItem(drops[0].name).type),1),new ItemStack(plot[0].itemValue,1));
    }
    internal static bool Commit(World world,EntityPlayer owner,AdvancedFarmingHarvestReceipt.Record r)
    {
        var c=Decode(r.Terminal);if(c==null)return true;var plot=new Vector3i(r.X,r.Y-1,r.Z);var operation=PlotOperation(r.Operation);var original=new BlockValue{rawData=c.Raw};
        if(AdvancedFarmingHarvestSettlement.HasCommit(world,plot,operation,c.Source,c.Raw,BlockValue.Air.rawData))return true;
        if(world.GetBlock(plot).rawData!=c.Raw||!AdvancedFarmingHarvestSettlement.MatchesSource(world,plot,c.Source.ToString("N"))||!world.GetBlock(plot+Vector3i.up).isair)return false;
        using(var mutation=AdvancedFarmingHarvestSettlement.BeginNativeMutation(world,plot,operation,c.Source,c.Raw,BlockValue.Air.rawData))
        {
        if(mutation==null)return false;
        Committing=true;committingWorld=world;committingPosition=plot;committingRaw=c.Raw;
        try{original.Block.DamageBlock(world,new BlockValueRef(plot),original,c.Damage,owner.entityId,null,c.HarvestTool,c.BypassMaxDamage);}
        finally
        {
            Committing=false;committingWorld=null;committingRaw=0;
            AdvancedFarmingHarvestSettlement.PublishNativeMutation(mutation);
        }
        return AdvancedFarmingHarvestSettlement.HasCommit(world,plot,operation,c.Source,c.Raw,BlockValue.Air.rawData);
        }
    }
    internal static Guid PlotOperation(Guid operation)
    {using(var hash=System.Security.Cryptography.SHA256.Create()){var digest=hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(operation.ToString("N")+":terminal-plot"));var bytes=new byte[16];Array.Copy(digest,bytes,16);return new Guid(bytes);}}
    internal static bool Committed(World world,AdvancedFarmingHarvestReceipt.Record r)
    {var c=Decode(r.Terminal);return c==null||AdvancedFarmingHarvestSettlement.HasCommit(world,new Vector3i(r.X,r.Y-1,r.Z),PlotOperation(r.Operation),c.Source,c.Raw,BlockValue.Air.rawData);}
    internal static bool SourceStillMatches(World world,AdvancedFarmingHarvestReceipt.Record r)
    {var c=Decode(r.Terminal);if(c==null)return true;var plot=new Vector3i(r.X,r.Y-1,r.Z);return world.GetBlock(plot).rawData==c.Raw&&AdvancedFarmingHarvestSettlement.MatchesSource(world,plot,c.Source.ToString("N"));}
    internal static void SourceRecorded(World world,AdvancedFarmingHarvestReceipt.Record r)
    {if(!string.IsNullOrEmpty(r.Terminal))AdvancedFarmingHarvestSettlement.SourceRecorded(world,new Vector3i(r.X,r.Y-1,r.Z),PlotOperation(r.Operation));}
    internal static UnityEngine.Vector3 DropPosition(AdvancedFarmingHarvestReceipt.Record r,ItemStack output,out UnityEngine.Vector3 extent)
    {
        extent=UnityEngine.Vector3.zero;var c=Decode(r.Terminal);
        if(c!=null){var plot=AdvancedFarmingHarvestReceipt.ReadOutputs(c.PlotOutputs);if(plot.Length==1&&RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(plot[0].itemValue,1),new ItemStack(output.itemValue,1))){extent=new UnityEngine.Vector3(0.5f,0.5f,0.5f);return new UnityEngine.Vector3(c.DropX,c.DropY,c.DropZ);}}
        return new UnityEngine.Vector3(r.DropX,r.DropY,r.DropZ);
    }
    internal static void Install(Harmony harmony)
    {RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(AdvancedFarmingHarvestTerminalHit));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(AdvancedFarmingHarvestTerminalDamageEvent));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(AdvancedFarmingHarvestTerminalNative));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(AdvancedFarmingHarvestTerminalCollect));}
}
[HarmonyPatch(typeof(GameUtils),nameof(GameUtils.HarvestOnAttack))]
internal static class AdvancedFarmingHarvestTerminalNative
{
    [HarmonyPriority(Priority.First)] private static void Prefix(ItemActionData _actionData,out object __state)=>__state=AdvancedFarmingHarvestTerminalPlot.Open(_actionData);
    private static Exception Finalizer(Exception __exception,object __state){AdvancedFarmingHarvestTerminalPlot.Close(__state,__exception);return __exception;}
}
[HarmonyPatch(typeof(GameUtils),nameof(GameUtils.collectHarvestedItem))]
internal static class AdvancedFarmingHarvestTerminalCollect
{
    [HarmonyPriority(Priority.First)] private static bool Prefix(ItemActionData _actionData,ItemValue _iv,int _count,float _prob,bool _bScaleCountOnDamage,ref bool __result)
    {if(!AdvancedFarmingHarvestTerminalPlot.Collect(_actionData,_iv,_count,_prob,_bScaleCountOnDamage,out var accepted))return true;__result=accepted;return false;}
}

// Scope is the exact original native Hit call, including nested calls/restoration.
[HarmonyPatch(typeof(ItemActionAttack),nameof(ItemActionAttack.Hit))]
internal static class AdvancedFarmingHarvestTerminalHit
{
    private static void Prefix(ItemActionAttack.AttackHitInfo _attackDetails,out object __state)=>__state=AdvancedFarmingHarvestTerminalPlot.EnterNativeHit(_attackDetails);
    private static Exception Finalizer(Exception __exception,object __state){AdvancedFarmingHarvestTerminalPlot.LeaveNativeHit(__state);return __exception;}
}
[HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.FireEvent),new[]{typeof(MinEventTypes),typeof(bool)})]
internal static class AdvancedFarmingHarvestTerminalDamageEvent
{
    private static bool Prefix(EntityAlive __instance,MinEventTypes _eventType,bool useInventory)=>!AdvancedFarmingHarvestTerminalPlot.SuppressHeldDamage(__instance,_eventType,useInventory);
}
