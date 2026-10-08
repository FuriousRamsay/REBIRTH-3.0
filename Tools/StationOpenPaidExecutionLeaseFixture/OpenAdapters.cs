using System;
class XUiC_WorkstationWindowGroup{public Window windowGroup=new();public Ui xui=new();public Model WorkstationData=new();}
class Window{public bool isShowing=true;}class Ui{public PlayerUi playerUI=new();}class PlayerUi{public EntityPlayer entityPlayer;}class Model{public TileEntityWorkstation TileEntity;}
static class RebirthStationLiveAccess{public static bool Locked=true;public static bool TryResolve(EntityPlayer p,Vector3i pos,string creation,out TileEntityWorkstation tile,out RebirthWorldCharacterRecord owner){tile=p?.world?.Station;owner=RebirthWorldCharacterService.Owner;return Locked&&p!=null&&!p.world.IsRemote()&&creation==owner.Origin.CreationId;}}
static class RebirthCookingBatch{public static bool IsBatch(Recipe r)=>r.Name=="cooking";}
class RebirthCapabilityEvaluation{public bool IsAllowed;}
static class RebirthCapabilityService{public static bool Allowed=true;public static RebirthCapabilityEvaluation EvaluateRecipe(EntityPlayer p,string recipe)=>new(){IsAllowed=Allowed};}
static class RebirthStationQueuedDiscoveryAuthorization{public static bool Discovery,Allowed=true;public static bool TryEvaluate(TileEntityWorkstation s,RecipeQueueItem q,EntityPlayer p,out bool allowed,out string reason){allowed=Allowed;reason=null;return Discovery;}}
class BlockValue{public BlockData Block=new();}class BlockData{public float HeatMapStrength;}
static class GameTimer{public static Timer Instance=new();}class Timer{public ulong ticks=200;}
static class Mathf{public static float Min(float a,float b)=>Math.Min(a,b);}enum EnumAIDirectorChunkEvent{Campfire}
partial class TileEntityWorkstation{
 public ulong lastTickTime=180;public bool isBesideWater;public float BurnTotalTimeLeft=10,FuelUsed,MaterialTime;public int LightUpdates,VisibleUpdates;public bool IsCrafting=>Queue.Length>0;
 public bool IsByWater(World w,Vector3i p)=>false;public void UpdateLightState(World w,BlockValue b){LightUpdates++;}public void HandleFuel(World w,float dt){FuelUsed+=dt;}public void emitHeatMapEvent(World w,EnumAIDirectorChunkEvent t){}public void HandleMaterialInput(float dt){MaterialTime+=dt;}public void setModified()=>SetModified();public void UpdateVisible(){VisibleUpdates++;}
 public int CandidateInsert(ItemStack[] cells,ItemStack stack,int max){var q=Queue[Queue.Length-1];return RebirthStationOpenPaidExecutionLease.AllowsInsertion(this,q)?RebirthStationPaidCompletionCallsite.Insert(cells,stack,max,this):-1;}
}
