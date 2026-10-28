// AUTHORED_NOT_RUN. Actual pump/projection/reply. Native scope/frame/UI are
// adapters. No financial delivery, saved publication or real endpoint claim.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Security.Cryptography;
namespace UnityEngine{static class Time{internal static float realtimeSinceStartup;}}
interface INetConnection{}
class Connection:INetConnection{}
class World{}
class Recipe{internal Guid Group;}
class RecipeQueueItem{internal Recipe Recipe;internal object RepairItem;internal int AmountToRepair,Multiplier=1,StartingEntityId;internal byte Quality;internal float CraftingTimeLeft,OneItemCraftTime;}
class ItemStack{internal int Count;internal static ItemStack[] Clone(ItemStack[] items)=>items.Select(item=>new ItemStack{Count=item.Count}).ToArray();}
class TileEntityWorkstation{internal int Version=1;internal RecipeQueueItem[] Queue=new[]{new RecipeQueueItem()};internal ItemStack[] Input=new[]{new ItemStack{Count=7}},Output=new[]{new ItemStack{Count=9}},Tools=new[]{new ItemStack{Count=1}};}
class StationData{internal TileEntityWorkstation TileEntity;}
class WindowGroup{internal bool isShowing=true;}
class XUiC_RecipeStack{internal bool IsCrafting,IsDirty,Allows=true;internal Recipe Recipe;internal bool SetRecipe(Recipe recipe,int count,float time,bool native,int owner,byte quality,float one){if(!Allows)return false;Recipe=recipe;return true;}}
class XUiC_CraftingQueue{internal XUiC_RecipeStack[] Cards=new[]{new XUiC_RecipeStack()};internal XUiC_RecipeStack[] GetRecipesToCraft()=>Cards;}
class Grid{internal ItemStack[] Slots=new[]{new ItemStack{Count=100}};internal bool Suppress;internal int Writes;internal Action During;internal void SetSlots(ItemStack[] values){Writes++;if(!Suppress)Slots=ItemStack.Clone(values);During?.Invoke();}internal ItemStack[] GetSlots()=>Slots;}
class XUiC_WorkstationInputGrid:Grid{}
class XUiC_WorkstationOutputGrid:Grid{}
class XUiC_WorkstationToolGrid:Grid{}
class XUiC_WorkstationWindowGroup{
    internal WindowGroup windowGroup=new WindowGroup();internal StationData WorkstationData=new StationData();internal long Session=1;
    internal XUiC_CraftingQueue Queue=new XUiC_CraftingQueue();internal XUiC_WorkstationInputGrid Input=new XUiC_WorkstationInputGrid();internal XUiC_WorkstationOutputGrid Output=new XUiC_WorkstationOutputGrid();internal XUiC_WorkstationToolGrid Tools=new XUiC_WorkstationToolGrid();
    internal T GetChildByType<T>()where T:class=>typeof(T)==typeof(XUiC_CraftingQueue)?Queue as T:typeof(T)==typeof(XUiC_WorkstationInputGrid)?Input as T:typeof(T)==typeof(XUiC_WorkstationOutputGrid)?Output as T:Tools as T;
}
static class RebirthRecyclerRuntime{internal static bool TryGroup(Recipe recipe,out Guid group){group=recipe?.Group??Guid.Empty;return group!=Guid.Empty;}}
static class RebirthProductiveEarnedUiDeferredTransport{internal static bool TryGetCurrentNativeUiSession(XUiC_WorkstationWindowGroup window,out long session){session=window.Session;return true;}}
static class RebirthProductiveEarnedObservationBudget{
    class Lease:IDisposable{public void Dispose(){}}
    internal static bool TryRetainDetached(long bytes,out IDisposable memory){memory=new Lease();return true;}
    internal static IDisposable TryEnterDecoder()=>new Lease();
}
static class RebirthWorkstationCompletionOutbox{internal static string CurrentGuid(World world)=>"world";}
static class RebirthStationPublicationRecord{internal static string Digest(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}}
static class RebirthProductiveEarnedNativeBody{
    internal class Image{internal RecipeQueueItem[] Queue;internal ItemStack[] Input,Output,Tools;}
    internal static TileEntityWorkstation Last;
    internal static bool TryCaptureUiProjection(TileEntityWorkstation station,RecipeQueueItem[] queue,ItemStack[] input,ItemStack[] output,object fuel,ItemStack[] tools,out byte[] before,out byte[] after,out int offset,out int later){Last=station;before=after=BitConverter.GetBytes(station.Version);offset=later=0;return true;}
    internal static bool MatchesCurrent(TileEntityWorkstation station,byte[] image,int offset)=>BitConverter.ToInt32(image,0)==station.Version;
    internal static bool TryRead(byte[] image,int offset,out Image native,out ulong elapsed){elapsed=0;native=new Image{Queue=Last.Queue,Input=ItemStack.Clone(Last.Input),Output=ItemStack.Clone(Last.Output),Tools=ItemStack.Clone(Last.Tools)};return true;}
}
internal static partial class RebirthRecyclerPlacedSettlement{
    private sealed class ViewedCompletion:IDisposable{
        internal World World=new World();internal TileEntityWorkstation Station;internal XUiC_WorkstationWindowGroup Window;internal INetConnection Connection=new Connection();internal Guid Operation=Guid.NewGuid(),Group=Guid.NewGuid();
        internal string Request="immutable-native-introduction";internal long Session=1;internal bool Terminal,Alive=true,Disposed;internal float Next;
        internal bool Current(bool window)=>Alive&&!Disposed&&(!window||Window.windowGroup.isShowing&&Window.Session==Session&&ReferenceEquals(Window.WorkstationData.TileEntity,Station));
        public void Dispose(){Disposed=true;}
    }
    private static readonly List<ViewedCompletion> viewedCompletions=new List<ViewedCompletion>();private static int Sends;
    private static void SendPlacedText(string text){Sends++;}
    private static bool SameOwnerGrid(ItemStack[] left,ItemStack[] right)=>left.Length==right.Length&&left.Select((item,index)=>item.Count==right[index].Count).All(value=>value);
    // ACTUAL_HANDOFF
    private static ViewedCompletion Setup(){viewedCompletions.Clear();Sends=0;UnityEngine.Time.realtimeSinceStartup=2;var value=new ViewedCompletion{Station=new TileEntityWorkstation(),Window=new XUiC_WorkstationWindowGroup()};value.Window.WorkstationData.TileEntity=value.Station;viewedCompletions.Add(value);return value;}
    private static void Need(bool value,string text){if(!value)throw new Exception(text);}
    private static XElement Reply(ViewedCompletion value)=>new XElement("recyclerViewedTerminal",new XAttribute("version",1),new XAttribute("operation",value.Operation.ToString("N")),new XAttribute("world","world"),new XAttribute("terminal",new string('A',64)),new XAttribute("request",RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(value.Request))));
    private static void Tick(ViewedCompletion value){UnityEngine.Time.realtimeSinceStartup+=2;PumpViewedCompletions(value.World);}
    internal static void ExerciseHandoff(){
        var value=Setup();Tick(value);Need(viewedCompletions.Count==1&&Sends==1&&value.Window.Output.Writes==0,"native replica without saved terminal released/projected viewer");
        Need(ObserveViewedTerminal(Reply(value),value.Connection)&&value.Terminal,"matching terminal not bound");
        value.Station.Queue[0].Recipe=new Recipe{Group=value.Group};Tick(value);Need(viewedCompletions.Count==1&&value.Window.Output.Writes==0&&Sends==1,"original paid group still present but viewer released");
        value.Station.Queue[0].Recipe=null;value.Station.Output[0].Count=19;Tick(value);
        Need(viewedCompletions.Count==0&&value.Disposed&&value.Window.Input.Slots[0].Count==7&&value.Window.Output.Slots[0].Count==19&&value.Window.Tools.Slots[0].Count==1,"current native terminal projection overwrote later legitimate state");
        Need(value.Window.Queue.Cards[0].Recipe==null&&value.Window.Queue.Cards[0].IsDirty,"old native queue card retained");
        value=Setup();value.Terminal=true;value.Window.Output.Suppress=true;Tick(value);Need(viewedCompletions.Count==1&&!value.Disposed,"unobserved output projection released viewer");
        value.Window.Output.Suppress=false;Tick(value);Need(viewedCompletions.Count==0&&Sends==0,"projection continuation resent financial introduction");
        value=Setup();value.Terminal=true;value.Window.Input.During=()=>value.Station.Version++;Tick(value);Need(viewedCompletions.Count==1&&value.Window.Output.Writes==0,"native frame change during projection accepted");
        value=Setup();value.Terminal=true;value.Window.windowGroup.isShowing=false;Tick(value);Need(viewedCompletions.Count==0&&value.Window.Output.Writes==0,"closed native window mutated");
        value=Setup();value.Terminal=true;value.Window.Session++;Tick(value);Need(viewedCompletions.Count==0&&value.Window.Output.Writes==0,"new UI session received old projection");
        value=Setup();var foreign=Reply(value);foreign.SetAttributeValue("request","foreign");ObserveViewedTerminal(foreign,value.Connection);Need(!value.Terminal,"foreign introduction terminal accepted");
        ObserveViewedTerminal(Reply(value),new Connection());Need(!value.Terminal,"foreign native connection terminal accepted");
        value=Setup();Tick(value);PumpViewedCompletions(value.World);Need(Sends==1,"one-second cadence ignored");
        Console.WriteLine("MP027 actual ordinary viewer terminal/projection/handoff methods; native scope, frame and UI boundaries are adapters");
    }
}
static class Program{public static void Main()=>RebirthRecyclerPlacedSettlement.ExerciseHandoff();}
