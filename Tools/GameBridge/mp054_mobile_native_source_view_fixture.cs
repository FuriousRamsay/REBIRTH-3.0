// AUTHORED_NOT_RUN. Entire readonly production source view. Actual native load,
// full native bag/module hashing and entity/manager identity are named adapters.
using System;using System.Collections.Generic;using System.Runtime.CompilerServices;
class EntityVehicle{}
internal static partial class RebirthCookingMillingPublication{
 private sealed class Read{internal string Path="vehicles.dat",Digest="file";}
 private sealed class Record{internal Read Read=new Read();internal string Entity="entity",Bag="bag";}
 private sealed class MobileLoadedSource{internal EntityVehicle Vehicle;internal Record Record=new Record();internal string Identity="vehicle",Modules="original";internal Dictionary<Guid,byte[]> Bodies=new Dictionary<Guid,byte[]>();internal bool Valid=true;internal bool Current()=>Valid;}
 private static readonly ConditionalWeakTable<EntityVehicle,MobileLoadedSource> mobileLoadedSources=new ConditionalWeakTable<EntityVehicle,MobileLoadedSource>();
 private static bool Modules=true,Bag=true;private static bool MobileNativeModulesCurrent(MobileLoadedSource value)=>Modules;private static bool MobileLoadedBagMatches(EntityVehicle vehicle,Record record)=>Bag;
 private static int checks;private static void Need(bool value,string text){if(!value)throw new Exception(text);checks++;}
 public static void Main(){
  var vehicle=new EntityVehicle();var installation=Guid.NewGuid();var original=new MobileLoadedSource{Vehicle=vehicle};original.Bodies[installation]=new byte[]{1,2};mobileLoadedSources.Add(vehicle,original);
  Need(TryObserveNativeLoadedVehicle(vehicle,out var source)&&source.OriginalCurrent()&&source.Current(),"actual retained native lineage");
  Need(source.TryCopyStationBody(installation,out var body)&&body.Length==2&&body[0]==1,"exact full native module snapshot");body[0]=9;Need(original.Bodies[installation][0]==1,"read-only clone cannot mutate original");
  original.Modules="next-saved-baseline";Need(source.Current()&&!source.OriginalCurrent()&&!source.TryCopyStationBody(installation,out _),"later mutable baseline cannot become old token original");original.Modules="original";
  Bag=false;Need(!source.OriginalCurrent(),"changed native bag refuses original");Bag=true;Modules=false;Need(!source.OriginalCurrent(),"changed rich module metadata refuses original");Modules=true;
  Need(!source.TryCopyStationBody(Guid.NewGuid(),out _),"foreign installation refused");
  mobileLoadedSources.Remove(vehicle);mobileLoadedSources.Add(vehicle,new MobileLoadedSource{Vehicle=vehicle});Need(!source.Current()&&source.Vehicle==null,"replacement actual native source retires old token");
  Need(!TryObserveNativeLoadedVehicle(new EntityVehicle(),out _),"equal vehicle shape cannot issue native source");
  Console.WriteLine("PASS "+checks+" actual readonly native mobile source assertions");
 }
}
// ACTUAL_MOBILE_VIEW
