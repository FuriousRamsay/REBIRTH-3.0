// AUTHORED_NOT_RUN. Exact pending-source handle transition; native producers
// are explicit doubles, not inferred current TE equality.
using System;using System.Linq;
class RecipeQueueItem{internal object Recipe=new object();}
class Block{internal string Name="recycler";internal string GetBlockName()=>Name;}
class TileEntityWorkstation{internal RecipeQueueItem[] Queue=new[]{new RecipeQueueItem()};internal Block block=new Block();internal int Position=7;internal int ToWorldPos()=>Position;}
class ClientInfo{internal object[] netConnection=new[]{new object()};}
class RemoteLoadedOwner{internal object Actor=new object();}
class LoadedStation{internal bool Valid=true,Adopted,Matches=true;internal Guid Operation;internal string Stage="before";internal object World;internal TileEntityWorkstation Station=new TileEntityWorkstation();internal bool Current()=>Valid;internal bool MatchesOriginal(byte[] bytes)=>Matches;}
class ClosedPendingPublication
{
 internal static bool CanCapture=true;internal static ClosedPendingPublication Last;
 internal bool Historical=true,Disposed,Preserved;internal LoadedStation Loaded;
 internal bool HistoricalSaved()=>Historical;
 internal static ClosedPendingPublication FromActualPendingLoad(Subject source,LoadedStation loaded)=>CanCapture?(Last=new ClosedPendingPublication{Loaded=loaded}):null;
 internal void PreserveUnadoptedLoad(LoadedStation loaded){Preserved=true;Loaded=null;}
 internal void Dispose(){Disposed=true;}
}
class AccessHold{internal bool Allowed=true;internal int Calls;internal bool RebindPendingClosedLoad(Subject source,LoadedStation loaded){Calls++;return Allowed;}}
class Subject
{
 internal object Attached;internal ClosedPendingPublication Pending=new ClosedPendingPublication();internal bool RecipientValid=true,Origin=true,NativeCurrent=true,Pair=false;
 internal object World=new object(),Recipient=new object(),Connection=new object();internal ClientInfo Peer=new ClientInfo();internal Guid OwnerGeneration,Operation=Guid.NewGuid();
 internal TileEntityWorkstation Station=new TileEntityWorkstation();internal RecipeQueueItem Row;internal object Recipe;internal byte[] Image=new byte[]{1};internal AccessHold Access=new AccessHold();
 internal bool NativeOriginCurrent()=>Origin;internal bool RecipientCurrent()=>RecipientValid;internal bool Current()=>NativeCurrent;
 private bool QualifyPendingReconnectedPair(LoadedStation loaded,RemoteLoadedOwner owner,out ClientInfo peer,out Guid generation)
 {peer=Pair&&owner!=null?new ClientInfo():null;generation=Pair?Guid.NewGuid():Guid.Empty;return peer!=null;}
 // PRODUCTION_REBIND
 static LoadedStation Load(Subject source)=>new LoadedStation{World=source.World,Operation=source.Operation};
 static void Check(bool value,string label){if(!value)throw new Exception(label);}
 static void Main()
 {
  var source=new Subject();source.Row=source.Station.Queue.Last();source.Recipe=source.Row.Recipe;var old=source.Pending;var loaded=Load(source);
  Check(source.RebindActualPendingLoad(loaded)&&ReferenceEquals(source.Station,loaded.Station)&&loaded.Adopted&&old.Disposed,"actual loaded source moves exact original handle");
  source=new Subject();loaded=Load(source);var oldStation=source.Station;old=source.Pending;source.Access.Allowed=false;
  Check(!source.RebindActualPendingLoad(loaded)&&ReferenceEquals(source.Station,oldStation)&&ReferenceEquals(source.Pending,old)&&!old.Disposed&&!loaded.Adopted&&
    ClosedPendingPublication.Last.Preserved,"failed access qualification rolls back own refs and preserves actual loaded original");
  source=new Subject();loaded=Load(source);loaded.World=new object();Check(!source.RebindActualPendingLoad(loaded),"foreign native World refused");
  source=new Subject();loaded=Load(source);loaded.Matches=false;Check(!source.RebindActualPendingLoad(loaded),"different full native original refused");
  source=new Subject();loaded=Load(source);source.RecipientValid=false;Check(!source.RebindActualPendingLoad(loaded),"new owner cannot come from getter/current equality");
  source.Pair=true;Check(source.RebindActualPendingLoad(loaded,new RemoteLoadedOwner())&&source.OwnerGeneration!=Guid.Empty,"actual native owner/load pair admits distinct owner generation");
  source=new Subject();loaded=Load(source);ClosedPendingPublication.CanCapture=false;oldStation=source.Station;
  Check(!source.RebindActualPendingLoad(loaded)&&ReferenceEquals(source.Station,oldStation)&&!loaded.Adopted,"publication budget/source denial preserves old original");
  Console.WriteLine("PASS closed pending actual hot rebind scenarios");
 }
}
