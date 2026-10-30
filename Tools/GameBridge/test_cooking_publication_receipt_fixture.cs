// Actual Watch/Published/hook bodies are injected by test_station_snapshot_evidence.mjs.
// The camper adapter is a double; no camper or native disk acceptance is inferred.
class XUiC_RecipeStack {}
static class RebirthCamperStationSaveEvidence
{
 internal sealed class Request:System.IDisposable {internal bool Saved,Disposed;public void Dispose(){Disposed=true;}}
 internal static bool TryObserve(Request r,out object proof){proof=null;return r.Saved;}
}
static class CookingPublicationProbe
{
 // ACTUAL_COOKING_WATCH
 // ACTUAL_COOKING_PUBLISHED
 static void Check(bool ok,string why){if(!ok)throw new System.Exception(why);}
 internal static void Run()
 {
  for(int mode=0;mode<5;mode++)
  {
   bool current=true;var world=new object();var chunk=new object();var snapshot=new object();var tile=new object();
   using(var watch=new Watch{Current=()=>current})
   {
    watch.Placed=RebirthStationSnapshotEvidence.Watch(world,chunk,1,2,null,tile,(i,q)=>i.Length==1&&q.Length==1);
    var request=watch.Placed;
    // ACTUAL_COOKING_HOOK
    var frame=RebirthStationSnapshotEvidence.Begin(snapshot,chunk,world,1,2,true);
    var stream=new System.IO.MemoryStream();stream.Write(new byte[]{116,116,99,0,47,0,0,0},0,8);
    long offset=RebirthStationSnapshotEvidence.StartStationSpan(tile,stream);stream.WriteByte(1);RebirthStationSnapshotEvidence.CaptureStationSpan(tile,stream,offset,true);
    offset=RebirthStationSnapshotEvidence.StartStationSpan(tile,stream);stream.WriteByte(2);RebirthStationSnapshotEvidence.CaptureStationSpan(tile,stream,offset,false);stream.Position=0;
    RebirthStationSnapshotEvidence.Serialized(chunk,stream);RebirthStationSnapshotEvidence.Finish(frame,stream,null);
    long generation=request.Generation;
    if(mode==2)watch.Dispose();
    if(mode==3)RebirthStationSnapshotEvidence.MarkPublished(request,snapshot,generation);
    else RebirthStationSnapshotEvidence.MarkPublished(request,snapshot,mode==4?generation+1:generation,"path","digest");
    // Native pool return can precede the next main-thread recipe/admission tick.
    RebirthStationSnapshotEvidence.Invalidate(snapshot);
    if(mode==1)current=false;
    bool accepted=Published(watch);
    Check(accepted==(mode==0),"cooking recycled/scope/closed/missing-readback/wrong-generation "+mode);
    if(mode==0)Check(watch.Placed==null,"positive cooking acknowledgement releases its shared request");
   }
  }
  var mobile=new RebirthCamperStationSaveEvidence.Request{Saved=true};
  using(var watch=new Watch{Current=()=>true,Mobile=mobile})Check(Published(watch)&&mobile.Disposed,"existing mobile publication path retained");
  System.Console.WriteLine("PASS actual cooking Watch/Published/hook: recycled positive, original scope, closed/missing/wrong generation, disposal and unchanged mobile adapter path (double)");
 }
}
