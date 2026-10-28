// Deferred actual raw-capture and queue witness validation. The native decoder
// and reader are explicit doubles; no claim of executed native parsing follows.
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
class ItemValue {public string Image;public int type=1;public bool IsEmpty()=>false;}
class PooledBinaryReader:BinaryReader {public PooledBinaryReader(Stream stream):base(stream){}}
static class RebirthNativeItemCodec {public static bool Valid=true;public static bool TryDecode(string image,out ItemValue value){value=Valid?new ItemValue{Image=image}:null;return Valid;}}
class Recipe {public bool IsScrap;public int count=1,itemValueType=1;}
class RecipeQueueItem {public ItemValue RepairItem;public int AmountToRepair=20,Multiplier=1;public Recipe Recipe=new Recipe();}
class CraftingData {public RecipeQueueItem[] RecipeQueueItems;}
class PlayerDataFile {public CraftingData craftingData=new CraftingData();}
static class Witness {
 private static string Image(ItemValue value)=>value.Image;
 private static readonly ConditionalWeakTable<PlayerDataFile,QueueWitness> NativeQueues=new ConditionalWeakTable<PlayerDataFile,QueueWitness>();
 [ThreadStatic]private static RawCapture rawCapture;
 [ThreadStatic]private static bool decodingRaw;
 // PRODUCTION_METHODS
 static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
 public static void Main(){
  var item=new ItemValue{Image="typed-image-without-original-texture"};var row=new RecipeQueueItem{RepairItem=item};var file=new PlayerDataFile();file.craftingData.RecipeQueueItems=new[]{row};
  Check(QueueImages(file).Length==0,"uncaptured native file supplied proof");Check(!TryQueueNativeItems(file,out _),"uncaptured queue was treated as empty for rejection");
  NativeQueues.Add(file,new QueueWitness{Images=new[]{"actual-full-native-texture-image:20"},Native=new[]{"actual-full-native-texture-image"},Rows=new[]{row},Items=new[]{item},Typed=new[]{item.Image},Shape=new[]{QueueShape(row)}});
  Check(QueueImages(file).Single()=="actual-full-native-texture-image:20","canonical capture replaced with lossy typed image");
  Check(TryQueueNativeItems(file,out var fullNative)&&fullNative.Single()=="actual-full-native-texture-image","raw queue item observation lost full texture");row.AmountToRepair=0;Check(!TryQueueNativeItems(file,out _),"changed amount produced rejection absence");Check(QueueImages(file).Length==0,"changed amount reused prior captured image");row.AmountToRepair=20;
  row.Recipe.count=2;Check(QueueImages(file).Length==0,"changed recipe reused prior captured image");row.Recipe.count=1;
  item.Image="mutated";Check(QueueImages(file).Length==0,"changed typed item reused prior captured image");item.Image="typed-image-without-original-texture";
  row.RepairItem=new ItemValue{Image=item.Image};Check(QueueImages(file).Length==0,"equal replacement item borrowed original byte capture");row.RepairItem=item;
  file.craftingData.RecipeQueueItems=new[]{new RecipeQueueItem{RepairItem=item}};Check(QueueImages(file).Length==0,"replacement row borrowed original capture");file.craftingData.RecipeQueueItems=new[]{row};
  Check(QueueImages(file).Length==1,"unchanged captured source stopped matching");
  var bytes=new byte[]{1,2,3,4,5};var capturedItem=new ItemValue{Image="lossy typed read"};var capturedFile=new PlayerDataFile();capturedFile.craftingData.RecipeQueueItems=new[]{new RecipeQueueItem{RepairItem=capturedItem}};
  using(var capture=new RawCapture())using(var stream=new MemoryStream(bytes))using(var reader=new PooledBinaryReader(stream)){
   long start=BeginRawItem(reader);reader.ReadBytes(bytes.Length);EndRawItem(reader,start,capturedItem);Check(stream.Position==bytes.Length,"raw capture shifted caller stream");capture.Bind(capturedFile);Check(QueueImages(capturedFile).Single()==Convert.ToBase64String(bytes)+":20","raw native bytes lost to typed image");
   decodingRaw=true;Check(BeginRawItem(reader)==-1,"canonical decoder recursed into raw capture");decodingRaw=false;
  }
  using(var capture=new RawCapture())using(var stream=new MemoryStream(bytes))using(var reader=new PooledBinaryReader(stream)){
   long start=BeginRawItem(reader);reader.ReadBytes(bytes.Length);RebirthNativeItemCodec.Valid=false;EndRawItem(reader,start,capturedItem);RebirthNativeItemCodec.Valid=true;capture.Bind(capturedFile);Check(capture.Failed&&QueueImages(capturedFile).Length==0&&stream.Position==bytes.Length,"invalid canonical bytes retained stale witness or shifted stream");
  }
  using(var capture=new RawCapture())using(var stream=new MemoryStream(bytes))using(var reader=new PooledBinaryReader(stream)){
   capture.Budget=2;long start=BeginRawItem(reader);reader.ReadBytes(bytes.Length);EndRawItem(reader,start,capturedItem);Check(capture.Failed,"raw capture budget bypassed");
  }
  var emptyFile=new PlayerDataFile();emptyFile.craftingData.RecipeQueueItems=new RecipeQueueItem[0];Check(!TryQueueNativeItems(emptyFile,out _),"unknown empty queue granted rejection proof");using(var emptyCapture=new RawCapture()){emptyCapture.Bind(emptyFile);}Check(TryQueueNativeItems(emptyFile,out var emptyItems)&&emptyItems.Length==0,"positively captured empty queue refused");Console.WriteLine("PASS actual raw capture/stream restoration/bounds and queue witness invalidation; canonical native decoder and reader are explicit doubles, equipment parser NOT exercised");
 }
}
