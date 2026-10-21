using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;

internal static partial class RebirthCookingMillingPublication
{
    [ThreadStatic]private static EntityVehicle millingVehicleTick;
    private static void ObserveMobileMillingTickReturned(EntityVehicle vehicle)
    {
        foreach(var original in pending.Where(value=>ReferenceEquals(value.Vehicle,vehicle)&&value.NativeAccepted&&!value.MobileTickReturned).ToArray())
        {
            if(!original.MetadataCurrent()||!original.NativeTerminal())continue;
            original.NativeAfter=RebirthCamperStationSnapshot.Capture(((RebirthCamperStationState)original.Station).InstallationId,(RebirthCamperStationState)original.Station);
            original.NativeAfterMode="RCS";original.MobileTickReturned=true;TryFinish(original);
        }
    }
    // This reader joins a returned native vehicle load with its actual restored
    // named module owner. It may publish terminal metadata; it never installs a
    // snapshot, generates output, clears a queue, or invokes a player callback.
    private sealed class MobileColdScan:IDisposable
    {
        internal IEnumerator<string> Files;internal bool Done,Blocked;
        internal readonly Dictionary<Guid,HashSet<Guid>> Holds=new Dictionary<Guid,HashSet<Guid>>();
        internal void Hold(Guid installation,Guid operation){if(!Holds.TryGetValue(installation,out var operations))Holds.Add(installation,operations=new HashSet<Guid>());operations.Add(operation);}
        internal void Release(Guid installation,Guid operation){if(Holds.TryGetValue(installation,out var operations)){operations.Remove(operation);if(operations.Count==0)Holds.Remove(installation);}}
        public void Dispose(){Files?.Dispose();Files=null;}
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityVehicle,MobileColdScan> mobileUnqualifiedScans=
        new System.Runtime.CompilerServices.ConditionalWeakTable<EntityVehicle,MobileColdScan>();
    private static bool HasMobileColdHold(TileEntityWorkstation station)
    {
        // Cold milling recovery belongs to the mortar station, not every station in its camper.
        if(station is RebirthCamperStationState other && other.InstalledBlock.Block.GetBlockName()!="WorkbenchMortarPestle001_FR")return false;
        if(station is not RebirthCamperStationState mobile||mobile.BoundVehicle==null||
            !mobileLoadedSources.TryGetValue(mobile.BoundVehicle,out var source))
            return station is RebirthCamperStationState unresolved&&unresolved.BoundVehicle!=null&&
                (!mobileUnqualifiedScans.TryGetValue(unresolved.BoundVehicle,out var scan)||!scan.Done||scan.Blocked||scan.Holds.ContainsKey(unresolved.InstallationId));
        return !source.Current()||source.Scan==null||!source.Scan.Done||source.Scan.Blocked||source.Scan.Holds.ContainsKey(mobile.InstallationId);
    }
    private static bool MobileNativeModulesCurrent(MobileLoadedSource source)
    {
        if(!source.Current())return false;var item=source.Vehicle.vehicle?.GetUpdatedItemValue();
        return item!=null&&item.TryGetMetadata("RebirthCamperIdentity",out string identity)&&identity==source.Identity&&
            item.TryGetMetadata("RebirthCamperModules",out string modules)&&modules==source.Modules;
    }
    private static bool MobileTerminalEnvelope(XElement root,Guid operation,World world,XElement original)
        =>root?.Name=="workstationCompletionIntent"&&(string)root.Attribute("operation")==operation.ToString("N")&&
            (string)root.Attribute("world")==RebirthWorkstationCompletionOutbox.CurrentGuid(world)&&
            root.Elements("before").Count()==1&&root.Element("before").Elements().Count()==1&&
            XNode.DeepEquals(root.Element("before").Element("cookingMillingOriginal"),original);
    private static IEnumerator<string> EnumerateMobileColdOriginals()
    {
        // Root validates containment and reparse safety. A new save legitimately
        // has no milling history yet; create its empty directory rather than
        // latching DirectoryNotFoundException as an unresolved paid original.
        string root=RebirthWorkstationCompletionOutbox.Root("cookingMilling");
        Directory.CreateDirectory(root);
        RebirthWorkstationCompletionPublication.RequireSafeDirectory(root);
        return Directory.EnumerateFiles(root,"*.xml").GetEnumerator();
    }
    private static bool CanTickUnqualifiedMobileSource(EntityVehicle vehicle,RebirthCamperModuleRuntime runtime)
    {
        var scan=mobileUnqualifiedScans.GetValue(vehicle,_=>new MobileColdScan());
        if(scan.Blocked)return false;if(scan.Done)return true;
        try
        {
            if(scan.Files==null)scan.Files=EnumerateMobileColdOriginals();
            for(int count=0;count<4;count++)
            {
                if(!scan.Files.MoveNext()){scan.Done=true;scan.Dispose();return true;}
                string path=scan.Files.Current;long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
                if(length<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(length*12+65536),out var memory)){scan.Blocked=true;return false;}
                using(memory)
                {
                    var root=RebirthWorkstationCompletionPublication.ReadPrimary(path);var image=root?.Element("before")?.Element("cookingMillingOriginal");
                    if(image==null||!Guid.TryParseExact((string)root.Attribute("operation"),"N",out var operation)||
                        Path.GetFileName(path)!=operation.ToString("N")+".xml"||root.Elements().Count()!=1)throw new InvalidDataException();
                    if(!MobileTerminalEnvelope(root,operation,vehicle.world,image)||(string)image.Attribute("world")!=RebirthCookingOutputDelivery.WorldKey(vehicle.world)||
                        !Guid.TryParseExact((string)image.Attribute("mobile"),"N",out var installation)||runtime.Stations.Find(installation)==null)continue;
                    var identity=(string)image.Attribute("vehicleIdentity");if(!string.IsNullOrEmpty(identity)&&identity!=runtime.Identity.ToString("N"))continue;
                    var intent=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(intent==null)throw new InvalidDataException();
                    var unused=RebirthWorkstationCompletionPublication.ReadPrimary(path+".unused");
                    var unusedAdmission=new XElement(root);unusedAdmission.Add(new XElement("cookingMillingUnusedAdmission"));
                    var unusedClose=new XElement(root);unusedClose.Add(new XElement("cookingMillingUnusedClose",new XAttribute("original",path),new XElement("immutableOriginal",Convert.ToBase64String(intent))));
                    if(XNode.DeepEquals(unused,unusedAdmission)||XNode.DeepEquals(unused,unusedClose))continue;
                    var saved=RebirthWorkstationCompletionPublication.ReadPrimary(path+".saved");var proof=saved?.Element("cookingMillingSavedVehicle");
                    var completed=RebirthWorkstationCompletionPublication.ReadPrimary(path+".completed");var projection=completed?.Element("cookingMillingNativeProjection");
                    if(MobileTerminalEnvelope(saved,operation,vehicle.world,image)&&MobileTerminalEnvelope(completed,operation,vehicle.world,image)&&
                        saved.Elements().Count()==2&&completed.Elements().Count()==2&&proof!=null&&projection!=null&&
                        (string)proof.Attribute("original")==path&&(string)projection.Attribute("original")==path&&
                        projection.Element("immutableOriginal")?.Value==Convert.ToBase64String(intent)&&
                        projection.Element("actualNativeAfter")?.Value==proof.Element("nativeBody")?.Value&&proof.Element("nativeBody")!=null&&
                        projection.Element("actualOutput")?.Value==image.Element("combinedOutput")?.Value&&proof.Element("combinedOutput")?.Value==image.Element("combinedOutput")?.Value&&
                        projection.Element("actualQueue")?.Value==image.Element("queueAfter")?.Value&&proof.Element("queue")?.Value==image.Element("queueAfter")?.Value)continue;
                    scan.Hold(installation,operation);
                }
            }
            return false;
        }
        catch{scan.Blocked=true;return false;}
    }
    private static void ObserveMobileColdTerminal(MobileLoadedSource source,RebirthCamperModuleRuntime runtime,string path)
    {
        long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(length<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(length*12+65536),out var memory))
            throw new InvalidDataException("Milling original cannot be read within its observation allocation");
        using(memory)
        {
            var root=RebirthWorkstationCompletionPublication.ReadPrimary(path);var image=root?.Element("before")?.Element("cookingMillingOriginal");
            if(image==null||!Guid.TryParseExact((string)root.Attribute("operation"),"N",out var operation)||
                Path.GetFileName(path)!=operation.ToString("N")+".xml"||root.Elements().Count()!=1||!MobileTerminalEnvelope(root,operation,source.World,image))throw new InvalidDataException();
            if((string)image.Attribute("world")!=RebirthCookingOutputDelivery.WorldKey(source.World)||
                !Guid.TryParseExact((string)image.Attribute("mobile"),"N",out var installation))return;
            var installed=runtime.Stations.Find(installation);if(installed==null)return;
            string recordedVehicle=(string)image.Attribute("vehicleIdentity");
            if(!string.IsNullOrEmpty(recordedVehicle)&&recordedVehicle!=runtime.Identity.ToString("N"))return;
            source.Scan.Hold(installation,operation);
            // Earlier originals did not record the enclosing native module
            // owner. An installation GUID alone cannot select a copied camper.
            if(string.IsNullOrEmpty(recordedVehicle))return;
            var intent=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
            var unused=RebirthWorkstationCompletionPublication.ReadPrimary(path+".unused");
            // Positive unused admission has no native effect and needs no queue
            // equivalence. An absent attempted record never proves unused.
            if(intent==null)return;
            var unusedAdmission=new XElement(root);unusedAdmission.Add(new XElement("cookingMillingUnusedAdmission"));
            var unusedClose=new XElement(root);unusedClose.Add(new XElement("cookingMillingUnusedClose",new XAttribute("original",path),new XElement("immutableOriginal",Convert.ToBase64String(intent))));
            if(XNode.DeepEquals(unused,unusedAdmission)||XNode.DeepEquals(unused,unusedClose))
            {source.Scan.Release(installation,operation);return;}
            var attempted=new XElement(root);attempted.Add(new XElement("cookingMillingAttempted"));
            if(!XNode.DeepEquals(attempted,RebirthWorkstationCompletionPublication.ReadPrimary(path+".attempted")))return;
            var saved=RebirthWorkstationCompletionPublication.ReadPrimary(path+".saved");
            var proof=saved?.Element("cookingMillingSavedVehicle");
            if(saved==null)
            {
                TryContinueMobileTilePrefix(source,runtime,installed.State,installed.InstalledItem,path,operation,image,intent);
                return;
            }
            if(!MobileTerminalEnvelope(saved,operation,source.World,image)||saved.Elements().Count()!=2||proof==null||
                (string)proof.Attribute("original")!=path||
                proof.Element("queue")?.Value!=image.Element("queueAfter")?.Value||
                proof.Element("combinedOutput")?.Value!=image.Element("combinedOutput")?.Value)return;
            var body=Convert.FromBase64String(proof.Element("nativeBody")?.Value??"");
            var output=Convert.FromBase64String(image.Element("combinedOutput")?.Value??"");
            var queue=Convert.FromBase64String(image.Element("queueAfter")?.Value??"");
            if(body.Length<1||output.Length<1||queue.Length<1||intent==null)return;
            var terminal=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(source.World)),new XElement("before",new XElement(image)),
                new XElement("cookingMillingNativeProjection",new XAttribute("original",path),new XAttribute("afterOffset",0),new XAttribute("afterMode","RCS"),
                    new XElement("actualNativeAfter",Convert.ToBase64String(body)),new XElement("immutableOriginal",Convert.ToBase64String(intent)),
                    new XElement("actualQueue",Convert.ToBase64String(queue)),new XElement("actualOutput",Convert.ToBase64String(output))));
            if(XNode.DeepEquals(terminal,RebirthWorkstationCompletionPublication.ReadPrimary(path+".completed")))
            {source.Scan.Release(installation,operation);return;}
            // The saved module bytes must be those returned by the actual native
            // reader and still belong to the actual restored installation.
            if(!MobileNativeModulesCurrent(source)||runtime.TransferPending||
                !ReferenceEquals(installed.State.BoundVehicle,source.Vehicle)||
                !RebirthCamperStationSnapshot.Capture(installation,installed.State).SequenceEqual(body)||
                !RebirthCamperInventorySnapshot.Capture(installed.State.Output).SequenceEqual(output)||!Queue(installed.State.Queue).SequenceEqual(queue)||
                !RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,intent))return;
            if(RebirthWorkstationCompletionPublication.Write(path+".completed",terminal)&&
                XNode.DeepEquals(terminal,RebirthWorkstationCompletionPublication.ReadPrimary(path+".completed"))&&MobileNativeModulesCurrent(source))
                source.Scan.Release(installation,operation);
        }
    }
    [HarmonyPatch(typeof(RebirthCamperModuleRuntime),"TickStations")]
    private static class MobileNativeMillingTick
    {
        private static bool Prefix(RebirthCamperModuleRuntime __instance,EntityVehicle ___vehicle,bool __runOriginal,out object __state)
        {
            __state=null;if(!__runOriginal)return true;
            // Each milling station protects its own queue in Held(); unrelated stations must keep ticking.
            if(!mobileLoadedSources.TryGetValue(___vehicle,out var source)||!source.Current())
            {CanTickUnqualifiedMobileSource(___vehicle,__instance);__state=millingVehicleTick;millingVehicleTick=___vehicle;return true;}
            try
            {
                if(source.Scan==null)source.Scan=new MobileColdScan{Files=EnumerateMobileColdOriginals()};
                var scan=source.Scan;
                if(scan.Blocked){__state=millingVehicleTick;millingVehicleTick=___vehicle;return true;}
                if(!scan.Done)
                {
                    if(!MobileNativeModulesCurrent(source)){__state=millingVehicleTick;millingVehicleTick=___vehicle;return true;}
                    for(int index=0;index<4;index++)
                    {
                        if(!scan.Files.MoveNext()){scan.Done=true;scan.Dispose();break;}
                        ObserveMobileColdTerminal(source,__instance,scan.Files.Current);
                    }
                }
                // Continue ticking ordinary stations while milling recovery scans old records.
                __state=millingVehicleTick;millingVehicleTick=___vehicle;return true;
            }
            catch{if(source.Scan!=null)source.Scan.Blocked=true;__state=millingVehicleTick;millingVehicleTick=___vehicle;return true;}
        }
        private static void Postfix(EntityVehicle ___vehicle,bool __runOriginal)
        {if(__runOriginal&&ReferenceEquals(millingVehicleTick,___vehicle))try{ObserveMobileMillingTickReturned(___vehicle);}catch{/* No returned complete tuple proof. */}}
        private static Exception Finalizer(EntityVehicle ___vehicle,Exception __exception,object __state)
        {if(ReferenceEquals(millingVehicleTick,___vehicle))millingVehicleTick=__state as EntityVehicle;return __exception;}
    }
}
