// AUTHORED_NOT_RUN. Complete actual StationEffectImage. Native FromPlayer,
// owner/body serializers, current source and create-only archive are adapters.
using System;using System.IO;using System.Linq;using System.Xml.Linq;using System.Collections.Generic;
class World{internal bool Remote;internal bool IsRemote()=>Remote;}
class Actor{internal int entityId=7;}
class ItemStack{internal byte Value;}
class TileEntityWorkstation{internal string Birth="birth";}
class Data{internal TileEntityWorkstation TileEntity=new TileEntityWorkstation();}
class XUiC_WorkstationWindowGroup{internal Data WorkstationData=new Data();}
class Card{internal XUiC_WorkstationWindowGroup Window=new XUiC_WorkstationWindowGroup();internal T GetParentByType<T>() where T:class=>Window as T;}
class PlayerDataFile{internal static bool Changed;internal int id=7,deaths=2;internal MemoryStream equipmentData=new MemoryStream(new byte[]{3});internal void FromPlayer(Actor actor){if(Changed)id++;}}
static class RebirthGearPlayerDataInventory{internal static ItemStack[] ReadSlots(PlayerDataFile data,bool bag)=>new[]{new ItemStack{Value=(byte)(bag?1:2)}};}
static class RebirthProductiveEarnedUiOwnerFile{internal static byte[] DeferredNativeOwnerImage(PlayerDataFile data)=>new byte[]{1,2,3,4,5,6};}
static class RebirthLogisticsEndpointIdentity{internal static bool TryGetIncarnation(World world,TileEntityWorkstation tile,out string birth){birth=tile.Birth;return true;}}
static class RebirthProductiveEarnedNativeBody{internal static bool Available=true;internal static bool TryCaptureCurrent(TileEntityWorkstation tile,out byte[] body,out int elapsed){body=new byte[]{8,9};elapsed=1;return Available;}}
static class RebirthProductiveEarnedObservationBudget{internal class Scope:IDisposable{public void Dispose(){}}internal static IDisposable TryEnterDecoder()=>new Scope();}
// ACTUAL_EFFECT
public static partial class RebirthRecipeOutputIntegration{
 private class UiScope{internal Card Card=new Card();}
 private class UiOutputOriginal{internal bool GridCallbackFence=true,Live=true;internal World World=new World();internal Actor Actor=new Actor();internal UiScope Scope=new UiScope();internal string StationBirth="birth",Path="original";internal int Deaths=2,StationQueueIndex=4;internal byte[] OwnerBeforeBag={1},OwnerBeforeBelt={2},OwnerBeforeEquipment={3};internal bool Current()=>Live;}
 private static byte[] OwningGrid(ItemStack[] items)=>items.Select(value=>value.Value).ToArray();
 private static string UiOriginalHash(byte[] bytes)=>Convert.ToBase64String(bytes);
 static Dictionary<string,byte[]> archives=new Dictionary<string,byte[]>();static bool Archive=true;
 private static bool PublishStationOutputOwnerNative(string path,byte[] native){if(!Archive)return false;if(archives.TryGetValue(path,out var saved))return saved.SequenceEqual(native);archives.Add(path,native.ToArray());return true;}
 static int checks;static void Need(bool condition,string label){checks++;if(!condition)throw new Exception(label);}
 public static void Main(){var source=new UiOutputOriginal();var row=new XElement("returned");Need(CaptureStationOutputReturnedEffect(source,row),"actual returned source not captured");var proof=row.Element("stationReturnedEffect");Need((int)proof.Attribute("queueIndex")==4&&(string)proof.Attribute("birth")=="birth"&&proof.Element("nativeBody")!=null,"effect lacks complete source body/index");Need(archives["original.effect-owner.native"].SequenceEqual(new byte[]{1,2,3,4,5,6}),"owner archive differs from actual capture");
  source.World.Remote=true;Need(!CaptureStationOutputReturnedEffect(source,new XElement("returned")),"remote replica qualified host native effect");source.World.Remote=false;source.Live=false;Need(!CaptureStationOutputReturnedEffect(source,new XElement("returned")),"stale source qualified");source.Live=true;
  source.Scope.Card.Window.WorkstationData.TileEntity.Birth="replacement";Need(!CaptureStationOutputReturnedEffect(source,new XElement("returned")),"replacement tile inherited native image");source.Scope.Card.Window.WorkstationData.TileEntity.Birth="birth";
  PlayerDataFile.Changed=true;Need(!CaptureStationOutputReturnedEffect(source,new XElement("returned")),"foreign captured actor qualified");PlayerDataFile.Changed=false;Archive=false;Need(!CaptureStationOutputReturnedEffect(source,new XElement("returned")),"failed archive qualified returned effect");Archive=true;RebirthProductiveEarnedNativeBody.Available=false;Need(!CaptureStationOutputReturnedEffect(source,new XElement("returned")),"missing full native station body qualified");Console.WriteLine("PASS "+checks+" actual returned effect-image assertions; native execution/serializers are adapters.");}
}
