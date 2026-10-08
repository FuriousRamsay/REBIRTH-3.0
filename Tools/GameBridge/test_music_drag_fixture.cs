using System;
using System.Collections.Generic;
namespace UnityEngine { public struct Vector2 {} }
enum EDragType { DragStart,DragEnd }
class XUiController { public bool Over; }
class EntityPlayerLocal {}
class PlayerUI { public EntityPlayerLocal entityPlayer=new EntityPlayerLocal(); }
class XUi { public PlayerUI playerUI=new PlayerUI(); }
class ItemValue { public ushort Seed; public string Metadata; public ItemValue(int type){} }
static class RebirthMusicLibraryService {public static string Encode(ItemValue item){return item?.Metadata??string.Empty;}}
static class RebirthConsoleInputGuardRuntime { public static bool BlocksGameplayInput(){return false;} }
static class RebirthMusicLibraryClient {
 public static long Generation=1,Revision=1;public static bool PendingTransfer;
 public static List<string> Items=new List<string>{"one","two"};public static int Requests;
 public static bool EnsureCurrent(EntityPlayerLocal player){return true;}
 public static void RequestReorder(EntityPlayerLocal player,int source,int target,long generation,long revision){Requests++;}
 public static void Dispatch(EntityPlayerLocal player,int operation,int index,ItemValue item=null){Requests++;}
}
class Check {
 struct AvailableCassette {public int Type;public ushort Seed;public string Name;public ItemValue Value;}
 readonly XUiController[] slots={new XUiController(),new XUiController()};
 readonly XUiController[] availableControls={new XUiController()};
 readonly List<AvailableCassette> available=new List<AvailableCassette>();
 XUi xui=new XUi();bool IsOpen=true;int page,dragStored=-1;AvailableCassette? dragCarried;
 long dragGeneration,dragRevision,displayedGeneration=1;EntityPlayerLocal dragOwner;int Renders;
 void CancelDrag(){dragStored=-1;dragCarried=null;dragOwner=null;}
 void Render(){Renders++;displayedGeneration=RebirthMusicLibraryClient.Generation;}
 static bool IsDropOver(XUiController c){return c.Over;}
 // SOURCE
 static void Assert(bool value,string why){if(!value)throw new Exception(why);}
 void Start(){CassetteDrag(slots[0],EDragType.DragStart,new UnityEngine.Vector2());}
 void End(){slots[1].Over=true;CassetteDrag(slots[0],EDragType.DragEnd,new UnityEngine.Vector2());}
 static void Main(){
  var c=new Check();RebirthMusicLibraryClient.Generation=2;c.Start();c.End();
  Assert(c.Renders==1&&RebirthMusicLibraryClient.Requests==0&&c.dragOwner==null,"stale rendered card refreshes without request");
  c.Start();c.End();Assert(RebirthMusicLibraryClient.Requests==1,"fresh drag still requests reorder");
  c.Start();RebirthMusicLibraryClient.Generation++;c.End();Assert(RebirthMusicLibraryClient.Requests==1,"generation change during drag cancels");
  c.Render();c.Start();RebirthMusicLibraryClient.Revision++;c.End();Assert(RebirthMusicLibraryClient.Requests==1,"revision change during drag cancels");
  c.Start();c.xui.playerUI.entityPlayer=new EntityPlayerLocal();c.End();Assert(RebirthMusicLibraryClient.Requests==1,"owner replacement cancels");
  c.Start();RebirthMusicLibraryClient.PendingTransfer=true;c.End();Assert(RebirthMusicLibraryClient.Requests==1&&c.dragOwner==null,"pending custody cancels and clears");
  RebirthMusicLibraryClient.PendingTransfer=false;c.Render();c.slots[1].Over=false;
  var value=new ItemValue(1){Seed=1,Metadata="selected-native-metadata"};var other=new ItemValue(1){Seed=1,Metadata="replacement-native-metadata"};
  var choice=new AvailableCassette{Type=1,Seed=1,Name="cassette",Value=value};c.available.Add(choice);
  Assert(!SameAvailable(choice,new AvailableCassette{Type=1,Seed=1,Name="cassette",Value=other}),"same type/seed changed native metadata refused");
  c.CassetteDrag(c.availableControls[0],EDragType.DragStart,new UnityEngine.Vector2());c.slots[0].Over=true;c.CassetteDrag(c.availableControls[0],EDragType.DragEnd,new UnityEngine.Vector2());Assert(RebirthMusicLibraryClient.Requests==2,"carried drag with detached exact item remains supported");  Console.WriteLine("PASS actual CassetteDrag: stale display, fresh gesture, generation/revision/owner changes and pending transfer; UI/network adapters doubled");
 }
}