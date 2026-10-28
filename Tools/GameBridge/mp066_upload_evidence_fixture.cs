// AUTHORED_NOT_RUN. Full production head-freeze helper; native queue codec is an adapter.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
class World {}
class EntityPlayer {internal int entityId=1;}
class ClientInfo {}
class ItemValue {internal string Value="original";}
class ItemStack {internal int count=1;internal ItemValue itemValue=new ItemValue();internal bool IsEmpty()=>count<=0;}
class PlayerDataFile {internal int id=1,deaths;internal ItemStack dragAndDropItem;internal string[] Native=new string[0],Queue=new string[0];internal bool RawReady=true;}
internal static partial class RebirthRepairOriginalCorrection {
 // ACTUAL_UPLOAD_TYPE
 private static string Image(ItemValue value)=>value.Value;
 private static bool TryQueueNativeItems(PlayerDataFile source,out string[] native){native=source.Native;return source.RawReady;}
 private static string[] QueueImages(PlayerDataFile source)=>source.Queue;
 private static void Need(bool value,string error){if(!value)throw new Exception(error);}
 private static Upload Make(){var result=new Upload{Source=new PlayerDataFile(),Player=new EntityPlayer(),BagSource=new MemoryStream(new byte[]{1,2,3}),BeltSource=new MemoryStream(new byte[]{4,5}),EquipmentSource=new MemoryStream(new byte[]{6,7}),NativeQueueItems=new string[0],Queue=new string[0]};result.BagSource.Position=2;result.BeltSource.Position=1;result.EquipmentSource.Position=1;return result;}
 internal static void Exercise(){
  var u=Make();Need(FreezeUploadHead(u)&&UploadHeadUnchanged(u),"unchanged accepted head refused");
  Need(u.BagSource.Position==2&&u.BeltSource.Position==1&&u.EquipmentSource.Position==1,"hashing changed native stream cursor");
  foreach(int area in new[]{0,1,2}){
   u=Make();Need(FreezeUploadHead(u),"fixture could not freeze head");var stream=area==0?u.BagSource:area==1?u.BeltSource:u.EquipmentSource;
   long position=stream.Position;stream.Position=0;stream.WriteByte(99);stream.Position=position;
   Need(!UploadHeadUnchanged(u)&&stream.Position==position,"in-place accepted blob mutation reused cached evidence");
  }
  u=Make();u.CursorSource=u.Source.dragAndDropItem=new ItemStack();Need(FreezeUploadHead(u),"cursor capture refused");
  u.Source.dragAndDropItem.itemValue.Value="other original";Need(!UploadHeadUnchanged(u),"same cursor reference changed native item");
  u=Make();Need(FreezeUploadHead(u),"empty cursor capture refused");u.Source.dragAndDropItem=new ItemStack();Need(!UploadHeadUnchanged(u),"new cursor original acquired empty-cursor evidence");
  u=Make();u.NativeQueueItems=new[]{"queued original"};u.Source.Native=new[]{"queued original"};u.Queue=new[]{"queued original:50"};u.Source.Queue=new[]{"queued original:50"};
  Need(FreezeUploadHead(u)&&UploadHeadUnchanged(u),"actual queued head refused");u.Source.Native[0]="different original";
  Need(!UploadHeadUnchanged(u),"in-place raw queue change reused accepted head");
  u=Make();Need(FreezeUploadHead(u),"head capture refused");u.Source.Queue=new[]{"new amount"};Need(!UploadHeadUnchanged(u),"queue shape change reused head");
  u=Make();Need(FreezeUploadHead(u),"head capture refused");u.Source.RawReady=false;Need(!UploadHeadUnchanged(u),"lost raw capture became positive absence");
  u=Make();Need(FreezeUploadHead(u),"head capture refused");u.Source.deaths++;Need(!UploadHeadUnchanged(u),"same head crossed native death episode");
  u=Make();Need(FreezeUploadHead(u),"head capture refused");u.Source.id++;Need(!UploadHeadUnchanged(u),"same head crossed native owner");
  u=Make();u.NativeQueueItems=null;Need(!FreezeUploadHead(u)&&!UploadHeadUnchanged(u),"unknown raw native queue admitted");
  u=Make();u.EquipmentSource=null;Need(!FreezeUploadHead(u)&&!UploadHeadUnchanged(u),"missing native blob admitted");
  u=Make();Need(FreezeUploadHead(u),"head capture refused");u.BagSource.Dispose();Need(!UploadHeadUnchanged(u),"disposed native body reused evidence");
  Console.WriteLine("PASS actual repair upload byte/cursor/queue evidence guard with native codec adapters; transport, persistence and gameplay NOT RUN");
 }
}
// ACTUAL_UPLOAD_EVIDENCE
static class Program {public static void Main()=>RebirthRepairOriginalCorrection.Exercise();}
