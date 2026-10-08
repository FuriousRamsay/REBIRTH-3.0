using System;using System.IO;
public class PlayerDataFile{public MemoryStream buffData=new(new byte[]{1});}
static class RebirthGearNativePlayerFile{public static bool Valid=true;public static PlayerDataFile Value=new();public static bool TryRead(RebirthStablePlayerIdentity owner,out PlayerDataFile file){file=Value;return Valid;}}
static class RebirthGearPreparationMarkerReader{public static bool Absent=true;public static bool HasNoOriginal(byte[] bytes)=>Absent;public static bool TryRead(byte[] bytes,out string marker,out Guid world,out int owned,out RebirthGearPreparationIntent intent){marker=ServerDoubles.Marker;intent=ServerDoubles.Intent;world=Guid.Empty;owned=4;return !Absent&&RebirthGearPreparationMarker.TryRead(marker,1,out world,out owned,out intent);}}
enum RebirthGearOwnerReceipt{Rejected}
static class RebirthGearReceiptReader{public static bool Rejected=true;public static bool Contains(byte[] bytes,string tx,RebirthGearOwnerReceipt r)=>Rejected;}
static class RebirthPlayerDataInventory{public static object ReadSlots(PlayerDataFile f,bool bag)=>new();}
public static partial class Program{
static int retiredChecks;
static void RetiredTests(RebirthGearPreparationRefusal refusal,RebirthGearInventorySnapshot image){
void R(bool b,string n){Check(b,n);retiredChecks++;}
void Reset(){RebirthGearNativePlayerFile.Valid=true;RebirthGearNativePlayerFile.Value=new();RebirthGearPreparationMarkerReader.Absent=true;RebirthGearReceiptReader.Rejected=true;ServerDoubles.Image=image;ServerDoubles.Upload=true;}
Reset();var owner=new RebirthStablePlayerIdentity();R(RebirthGearPreparationRefusalPlayerFileWitness.HasRetired(owner,refusal),"retired exact image predicate");
foreach(var fault in new[]{"file","buffnull","bufflarge","marker","receipt","inventory","capture"}){
Reset();switch(fault){case "file":RebirthGearNativePlayerFile.Valid=false;break;case "buffnull":RebirthGearNativePlayerFile.Value.buffData=null;break;case "bufflarge":RebirthGearNativePlayerFile.Value.buffData=new(new byte[1024*1024+1]);break;case "marker":RebirthGearPreparationMarkerReader.Absent=false;break;case "receipt":RebirthGearReceiptReader.Rejected=false;break;case "inventory":ServerDoubles.Image=null;break;case "capture":ServerDoubles.Upload=false;break;}
R(!RebirthGearPreparationRefusalPlayerFileWitness.HasRetired(owner,refusal),"retired refuses "+fault);}
Reset();R(!RebirthGearPreparationRefusalPlayerFileWitness.HasRetired(null,refusal)&&!RebirthGearPreparationRefusalPlayerFileWitness.HasRetired(owner,null),"retired nulls");
}
}
