// AUTHORED_NOT_RUN. Native world/marker is an explicit adapter. The complete
// production outbox, saved binding codec and binding/reentry partial run here.
// This does not certify actual native region/PDS lifecycle or target b259.
internal sealed partial class World {internal TileEntityWorkstation Station;internal TileEntityWorkstation GetTileEntity(int position)=>position==1?Station:null;}
internal sealed partial class TileEntityWorkstation {internal string Birth=Guid.NewGuid().ToString("N");internal Block block=new Block();}
internal sealed class Block {internal string GetBlockName()=>"station";}
internal sealed partial class RebirthWorkstationCompletionOriginal {internal int Position=1;internal string Block="station";}
internal static class RebirthLogisticsEndpointIdentity {
 internal static bool TryGetIncarnation(World world,TileEntityWorkstation tile,out string birth){birth=tile?.Birth;return ReferenceEquals(GameManager.Instance.World,world)&&!world.IsRemote()&&ReferenceEquals(world.Station,tile)&&birth!=null;}
}
internal static class Program {
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static void ClearOnlyFixtureCache(){var field=typeof(RebirthWorkstationCompletionOutbox).GetField("active",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);((System.Collections.IDictionary)field.GetValue(null)).Clear();}
 private static RebirthWorkstationCompletionOutbox.State Cold(string operation){ClearOnlyFixtureCache();return RebirthWorkstationCompletionOutbox.Pending("server").Single(value=>value.Original.Operation==operation);}
 public static void Main(){
  var world=new World{Station=new TileEntityWorkstation()};GameManager.Instance.World=world;
  var acceptedOriginal=new RebirthWorkstationCompletionOriginal{DeliveryBirth=world.Station.Birth};
  var recovered=RebirthWorkstationCompletionOutbox.Retain(acceptedOriginal,"server",acceptedStationBirth:acceptedOriginal.DeliveryBirth);
  Need(recovered!=null&&recovered.Saved&&recovered.NativeStation==null&&recovered.NativeStationBirth==acceptedOriginal.DeliveryBirth,"accepted original lost birth before first outbox publication");
  Need(RebirthWorkstationCompletionOutbox.TryRestoreUnattemptedNativeStation(recovered),"accepted-original description could not rebind actual same-birth source");
  Need(RebirthWorkstationCompletionOutbox.Retain(acceptedOriginal,"server",acceptedStationBirth:Guid.NewGuid().ToString("N"))==null,"foreign accepted description replaced immutable original birth");
  var state=RebirthWorkstationCompletionOutbox.Retain(new RebirthWorkstationCompletionOriginal(),"server",world,world.Station);
  Need(state!=null&&state.Saved&&state.NativeStationBirth==world.Station.Birth,"actual supplied source birth not persisted before native custody release");
  string birth=state.NativeStationBirth,operation=state.Original.Operation;
  state=Cold(operation);Need(state.NativeWorld==null&&state.NativeStation==null&&state.NativeStationBirth==birth,"saved description fabricated cold native handles or lost original birth");
  world=new World{Station=new TileEntityWorkstation{Birth=birth}};GameManager.Instance.World=world;
  Need(RebirthWorkstationCompletionOutbox.TryRestoreUnattemptedNativeStation(state)&&ReferenceEquals(state.NativeStation,world.Station),"exact persisted same-birth native source not rebound");
  state.NativeWorld=null;state.NativeStation=null;world.Station.Birth=Guid.NewGuid().ToString("N");
  Need(!RebirthWorkstationCompletionOutbox.TryRestoreUnattemptedNativeStation(state)&&state.NativeStation==null,"same-shaped replaced source issued material authority");world.Station.Birth=birth;
  state.ReagentAttempted=true;Need(!RebirthWorkstationCompletionOutbox.TryRestoreUnattemptedNativeStation(state),"cold attempted effect gained mutation permit");state.ReagentAttempted=false;
  ThreadManager.Main=false;Need(!RebirthWorkstationCompletionOutbox.TryRestoreUnattemptedNativeStation(state),"worker rebound native station");ThreadManager.Main=true;
  var exact=RebirthWorkstationCompletionPublication.ReadPrimary(state.Path);var changed=new XElement(exact);changed.SetAttributeValue("decision",true);changed.Save(state.Path);
  Need(!RebirthWorkstationCompletionOutbox.TryRestoreUnattemptedNativeStation(state),"changed saved stage rebound source from matching digest");exact.Save(state.Path);
  Need(RebirthWorkstationCompletionOutbox.TryRestoreUnattemptedNativeStation(state),"restored exact saved image could not bind same original");
  state.NativeWorld=null;state.NativeStation=null;state.NativeStationBirth=null;Need(!RebirthWorkstationCompletionOutbox.TryRestoreUnattemptedNativeStation(state),"legacy absent native binding was inferred from shape");
  var corrupt=new XElement(exact);corrupt.SetAttributeValue("nativeStationBirth","unknown");corrupt.Save(state.Path);ClearOnlyFixtureCache();Need(!RebirthWorkstationCompletionOutbox.Pending("server").Any(value=>value.Original.Operation==operation),"malformed saved station binding accepted");
  Console.WriteLine("PASS actual outbox native binding/reentry; native marker adapter, real region lifecycle NOT_RUN");
 }
}
