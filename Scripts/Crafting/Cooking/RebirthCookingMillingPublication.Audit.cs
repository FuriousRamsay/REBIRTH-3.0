using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

internal static partial class RebirthCookingMillingPublication
{
    private sealed class MillingAuditOriginal
    {internal World World;internal Guid Operation;internal string Path,Hash;internal WeakReference<TileEntityWorkstation> Station;}
    private static readonly Queue<MillingAuditOriginal> millingAuditOriginals=new Queue<MillingAuditOriginal>();
    private static void ObserveMillingAuditOriginal(Original original)
    {
#if REBIRTH_DEBUG
        if(GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||original.Intent==null)return;
        var previous=millingAuditOriginals.FirstOrDefault(value=>ReferenceEquals(value.World,original.World)&&value.Operation==original.Operation);
        if(previous!=null){previous.Station=new WeakReference<TileEntityWorkstation>(original.Station);return;}
        millingAuditOriginals.Enqueue(new MillingAuditOriginal{World=original.World,Operation=original.Operation,Path=original.Path,
            Hash=RebirthWorkstationCompletionOriginal.Hash(Convert.ToBase64String(original.Intent)),Station=new WeakReference<TileEntityWorkstation>(original.Station)});
        while(millingAuditOriginals.Count>65)millingAuditOriginals.Dequeue();
#endif
    }
    private static void ObserveMillingAuditSource(StaticSourcePurpose source)
    {
#if REBIRTH_DEBUG
        if(GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||millingAuditOriginals.Any(value=>ReferenceEquals(value.World,source.World)&&value.Operation==source.Operation))return;
        millingAuditOriginals.Enqueue(new MillingAuditOriginal{World=source.World,Operation=source.Operation,Path=source.Path,Hash=source.Hash,Station=new WeakReference<TileEntityWorkstation>(null)});
        while(millingAuditOriginals.Count>65)millingAuditOriginals.Dequeue();
#endif
    }
    internal static JObject CaptureMillingAudit(EntityPlayerLocal actor)
    {
        var result=new JObject{["records"]=new JArray(),["staticCold"]=new JArray()};
#if REBIRTH_DEBUG
        if(actor==null||!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||!ReferenceEquals(actor.world,GameManager.Instance?.World))return result;
        var records=(JArray)result["records"];
        foreach(var source in millingAuditOriginals.Where(value=>ReferenceEquals(value.World,actor.world)).Take(65))
        {
            var row=new JObject{["operation"]=source.Operation.ToString("N"),["original"]=source.Hash,["unknown"]=true};records.Add(row);
            IDisposable memory=null;
            try
            {
                long length=RebirthWorkstationCompletionPublication.PublishedLength(source.Path);
                if(length<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(20L*length+65536),out memory))continue;
                var root=RebirthWorkstationCompletionPublication.ReadPrimary(source.Path);var image=root?.Element("before")?.Element("cookingMillingOriginal");
                var intent=RebirthWorkstationCompletionPublication.CopyPublishedBytes(source.Path);
                if(image==null||intent==null||!MobileTerminalEnvelope(root,source.Operation,actor.world,image)||
                    RebirthWorkstationCompletionOriginal.Hash(Convert.ToBase64String(intent))!=source.Hash)continue;
                row["unknown"]=false;row["mobile"]=!string.IsNullOrEmpty((string)image.Attribute("mobile"));
                var active=pending.FirstOrDefault(value=>value.Operation==source.Operation&&ReferenceEquals(value.World,actor.world));
                row["active"]=active!=null;row["nativeAccepted"]=active?.NativeAccepted==true;
                row["nativeTickReturned"]=active==null||active.MobileTickReturned&&active.StationTickReturned;
                var saved=RebirthWorkstationCompletionPublication.ReadPrimary(source.Path+".saved");
                var proof=saved?.Element((bool)row["mobile"]?"cookingMillingSavedVehicle":"cookingMillingSavedTile");
                var completed=RebirthWorkstationCompletionPublication.ReadPrimary(source.Path+".completed");var terminal=completed?.Element("cookingMillingNativeProjection");
                bool savedValid=MobileTerminalEnvelope(saved,source.Operation,actor.world,image)&&saved.Elements().Count()==2&&proof!=null&&
                    (string)proof.Attribute("original")==source.Path&&proof.Element("queue")?.Value==image.Element("queueAfter")?.Value&&
                    proof.Element("combinedOutput")?.Value==image.Element("combinedOutput")?.Value&&proof.Element("nativeBody")!=null;
                row["nativeSaved"]=savedValid;
                row["coldNativeContinuation"]=false;
                foreach(var entryPath in Directory.EnumerateFiles(Path.GetDirectoryName(source.Path),Path.GetFileName(source.Path)+((bool)row["mobile"]?".cold-prefix-*":".native-prefix-*")).Take(65))
                {
                    var entry=RebirthWorkstationCompletionPublication.ReadPrimary(entryPath);
                    if(MobileTerminalEnvelope(entry,source.Operation,actor.world,image)&&entry.Elements().Count()==2&&
                        entry.Element((bool)row["mobile"]?"cookingMillingColdNativePrefix":"cookingMillingStaticNativePrefix")?.Element("nativeLoadedBody")!=null)
                    {row["coldNativeContinuation"]=true;break;}
                }
                row["terminalSaved"]=savedValid&&MobileTerminalEnvelope(completed,source.Operation,actor.world,image)&&completed.Elements().Count()==2&&terminal!=null&&
                    (string)terminal.Attribute("original")==source.Path&&terminal.Element("immutableOriginal")?.Value==Convert.ToBase64String(intent)&&
                    terminal.Element("actualOutput")?.Value==image.Element("combinedOutput")?.Value&&terminal.Element("actualQueue")?.Value==image.Element("queueAfter")?.Value;
                bool current=source.Station.TryGetTarget(out var station)&&
                    (station is RebirthCamperStationState mobile?mobile.BoundVehicle!=null&&ReferenceEquals(actor.world.GetEntity(mobile.BoundVehicle.entityId),mobile.BoundVehicle)&&
                        RebirthCamperModuleRuntime.TryGetExisting(mobile.BoundVehicle,out var runtime)&&ReferenceEquals(runtime.Stations.Find(mobile.InstallationId)?.State,mobile):
                        ReferenceEquals(actor.world.GetTileEntity(station.ToWorldPos()),station));
                row["nativeOutputQueueMatches"]=current&&Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(station.Output))==image.Element("combinedOutput")?.Value&&
                    Convert.ToBase64String(Queue(station.Queue))==image.Element("queueAfter")?.Value;
            }
            catch{/* Read-only bounded audit; no load/admission/setter. */}
            finally{memory?.Dispose();}
        }
        var cold=(JArray)result["staticCold"];
        foreach(var source in staticMillingSources.Where(value=>ReferenceEquals(value.World,actor.world)).Take(65))
            cold.Add(new JObject{["operation"]=source.Operation.ToString("N"),["original"]=source.Hash,["adopted"]=source.Adopted,
                ["nativeCurrent"]=source.Prefixes.Count(value=>value.Native?.Current()==true),["held"]=!source.Closed});
#endif
        return result;
    }
}
