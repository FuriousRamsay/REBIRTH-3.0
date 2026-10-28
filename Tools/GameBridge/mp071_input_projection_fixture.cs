using System;
using System.Collections.Generic;
// Actual local mark/pending/update and queue-write boundary. Native session,
// UI readback, current authority, serializers and processing are adapters.
namespace UnityEngine{public static class Time{public static float realtimeSinceStartup;}}
public class EntityVehicle{}
public class Recipe{public Guid Job;public bool Paid;}
public class RecipeQueueItem{public Recipe Recipe;}
public class RebirthCamperStationState{public EntityVehicle BoundVehicle;public Guid InstallationId;public RecipeQueueItem[] Queue;}
public class XUiM_Workstation{public object TileEntity;}
public class GUIWindow{}
public class Manager{public int Closed;public void Close(GUIWindow w){Closed++;}}
public class PlayerUi{public Manager windowManager=new Manager();}
public class XUi{public PlayerUi playerUI=new PlayerUi();}
public class XUiC_WorkstationWindowGroup{public XUi xui=new XUi();public GUIWindow windowGroup=new GUIWindow();}
public static class Log{public static void Warning(string message){}}
public static class RebirthCamperStationFuelOriginal{public static bool Held;public static bool IsHeld(RebirthCamperStationState s)=>Held;}
public static class RebirthCamperStationQueueProjection{public static int Count;public static bool Fail;public static void Refresh(XUiC_WorkstationWindowGroup w,RebirthCamperStationState s){Count++;if(Fail)throw new InvalidOperationException("readback failure");}public static bool HasPaid(RecipeQueueItem[] q){if(q!=null)foreach(var row in q)if(row?.Recipe?.Paid==true)return true;return false;}}
public static class RebirthCamperStationOpenQueueProcessing{public static bool Owned;public static bool OwnsQueue(RebirthCamperStationState s)=>Owned;}
public static class RebirthCamperCookingQueueAdmission{public static bool Allowed;public static bool CanWrite(RebirthCamperStationState s,RecipeQueueItem[] q)=>Allowed;}
public static class RebirthCamperStationNativeCrafting{public static bool TryJob(Recipe r,out Guid id){id=r?.Job??Guid.Empty;return id!=Guid.Empty;}}
public static class RebirthCamperStationWindowSession{
 class Session{public RebirthCamperStationState State;public bool Remote,Closing,Live=true,OriginalProjectionPending;public float NextOriginalProjection;}
 static Dictionary<XUiC_WorkstationWindowGroup,Session> sessions=new Dictionary<XUiC_WorkstationWindowGroup,Session>();
 static Dictionary<EntityVehicle,Dictionary<Guid,XUiC_WorkstationWindowGroup>> leases=new Dictionary<EntityVehicle,Dictionary<Guid,XUiC_WorkstationWindowGroup>>();
 static bool IsCurrent(Session s)=>s.Live&&!s.Closing;
// ACTUAL_MARK
// ACTUAL_PENDING
// ACTUAL_UPDATE
 public static XUiC_WorkstationWindowGroup Bind(RebirthCamperStationState s,bool remote=false){var w=new XUiC_WorkstationWindowGroup();sessions[w]=new Session{State=s,Remote=remote};leases[s.BoundVehicle]=new Dictionary<Guid,XUiC_WorkstationWindowGroup>{{s.InstallationId,w}};return w;}
 public static void Expire(XUiC_WorkstationWindowGroup w){sessions[w].Live=false;}
}
public static class QueueWriter{
// ACTUAL_WRITE
 public static bool Write(RebirthCamperStationState s,RecipeQueueItem[] q)=>Prefix(new XUiM_Workstation{TileEntity=s},q);
}
public static class Program{
 static void Need(bool v,string reason){if(!v)throw new Exception(reason);}
 static RebirthCamperStationState State()=>new RebirthCamperStationState{BoundVehicle=new EntityVehicle(),InstallationId=Guid.NewGuid(),Queue=new[]{new RecipeQueueItem()}};
 public static void Main(){
  var state=State();var w=RebirthCamperStationWindowSession.Bind(state);RebirthCamperStationWindowSession.MarkOriginalProjection(state);Need(RebirthCamperStationWindowSession.HasOriginalProjection(state)&&!QueueWriter.Write(state,state.Queue),"stale local queue write passed pending projection");
  RebirthCamperStationQueueProjection.Fail=true;Need(RebirthCamperStationWindowSession.BeforeUpdate(w)&&RebirthCamperStationWindowSession.HasOriginalProjection(state),"failed readback released pending projection");int calls=RebirthCamperStationQueueProjection.Count;RebirthCamperStationWindowSession.BeforeUpdate(w);Need(RebirthCamperStationQueueProjection.Count==calls,"failed projection retried every frame");
  UnityEngine.Time.realtimeSinceStartup+=1;RebirthCamperStationQueueProjection.Fail=false;Need(RebirthCamperStationWindowSession.BeforeUpdate(w)&&!RebirthCamperStationWindowSession.HasOriginalProjection(state)&&QueueWriter.Write(state,state.Queue),"successful native projection failed to release local gesture gate");
  RebirthCamperStationWindowSession.MarkOriginalProjection(state);RebirthCamperStationFuelOriginal.Held=true;UnityEngine.Time.realtimeSinceStartup+=1;calls=RebirthCamperStationQueueProjection.Count;RebirthCamperStationWindowSession.BeforeUpdate(w);Need(RebirthCamperStationQueueProjection.Count==calls&&!QueueWriter.Write(state,state.Queue),"projection crossed financial save custody");RebirthCamperStationFuelOriginal.Held=false;
  var remote=State();var rw=RebirthCamperStationWindowSession.Bind(remote,true);RebirthCamperStationWindowSession.MarkOriginalProjection(remote);Need(!RebirthCamperStationWindowSession.HasOriginalProjection(remote),"local projection gate captured remote presentation");
  var foreign=new RebirthCamperStationState{BoundVehicle=state.BoundVehicle,InstallationId=state.InstallationId,Queue=state.Queue};Need(!RebirthCamperStationWindowSession.HasOriginalProjection(foreign),"replacement state inherited local UI gate");
  RebirthCamperStationWindowSession.Expire(w);Need(!RebirthCamperStationWindowSession.HasOriginalProjection(state)&&!RebirthCamperStationWindowSession.BeforeUpdate(w)&&w.xui.playerUI.windowManager.Closed==1,"expired local session retained settled projection custody");
  var plain=State();var marked=new[]{new RecipeQueueItem{Recipe=new Recipe{Job=Guid.NewGuid()}}};Need(!QueueWriter.Write(plain,marked),"UI injected native paid row");plain.Queue=marked;Need(!QueueWriter.Write(plain,new[]{new RecipeQueueItem()}),"UI erased native paid row");RebirthCamperStationOpenQueueProcessing.Owned=true;RebirthCamperCookingQueueAdmission.Allowed=true;Need(QueueWriter.Write(plain,plain.Queue),"actual processing owner refused");RebirthCamperCookingQueueAdmission.Allowed=false;Need(!QueueWriter.Write(plain,plain.Queue),"foreign processing owner passed");
  Console.WriteLine("PASS actual local original projection and native queue write boundary; native UI rendering/persistence remain separate evidence");
 }
}
