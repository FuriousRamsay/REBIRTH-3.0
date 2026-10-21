using System;
using System.Diagnostics;
#if REBIRTH_DEBUG
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
#endif

internal static partial class RebirthProductiveEarnedCoordinator
{
    [Conditional("REBIRTH_DEBUG")]
    private static void RecordMobileReleasedStatus(Attempt original)
    {
#if REBIRTH_DEBUG
        if(!MobileReleasedCurrent(original.Artifact.OriginalPath))return;
        ObserveMobileAuditWorld();
        var row=MobileStatusRow(original);
        row["released"]=true;row["held"]=false;row["originalMarkerRetired"]=true;
        if(mobileReleasedRows.Count>=64)mobileReleasedRows.Remove(mobileReleasedRows.Keys.First());
        mobileReleasedRows[original.Frame.Operation.ToString("N")]=row;
#endif
    }
#if REBIRTH_DEBUG
    private static readonly Dictionary<string,JObject> mobileReleasedRows=new Dictionary<string,JObject>();
    private static World mobileAuditWorld;
    private static void ObserveMobileAuditWorld()
    {if(ReferenceEquals(mobileAuditWorld,GameManager.Instance?.World))return;mobileReleasedRows.Clear();mobileAuditWorld=GameManager.Instance?.World;}
    private static JObject MobileStatusRow(Attempt original)
    {
        var station=(RebirthCamperStationState)original.Station;var terminal=original.MobileTerminal;var successor=original.MobileSuccessor;
        var row=new JObject{["operation"]=original.Frame.Operation.ToString("N"),["birth"]=original.Frame.Birth.ToString("N"),
            ["digest"]=original.Artifact==null?null:Convert.ToBase64String(original.Artifact.OriginalDigest()),
            ["vehicle"]=RebirthCamperModuleRuntime.For(station.BoundVehicle).Identity.ToString("N"),
            ["mobile"]=true,["fullCountOrigin"]=original.Frame.HasMobileOrigin,
            ["initialReturned"]=original.MobileReturnedImage!=null&&!original.MobileAwaitingReturn||original.MobileInitialSaved,
            ["initialSaved"]=original.MobileInitialSaved,["returnedUnknown"]=original.MobileReturnUnknown,
            ["outputTimerObserved"]=original.OutputTimerObserved,["returnedTerminal"]=terminal?.Plan!=null,
            ["terminalSaved"]=terminal?.Positive==true,["pendingWork"]=original.PendingTimerObserved,
            ["pendingAmbiguous"]=original.PendingTimerAmbiguous,["localToolsPending"]=terminal?.PendingLocalTools!=null,
            ["toolsSaved"]=terminal?.ToolRecord!=null||terminal?.RemoteTools.Count>0,
            ["remoteToolsSources"]=terminal?.RemoteTools.Count??0,["remoteToolsPending"]=terminal?.PendingRemoteTools!=null,
            ["toolsReentryEntered"]=terminal?.PendingLocalTools?.Entered==true,
            ["successorEntered"]=successor?.Entered==true,["successorReturned"]=successor?.Returned==true,
            ["childPrepared"]=successor?.Next!=null,["clearing"]=terminal?.Clearing==true,
            ["released"]=false,["held"]=true,["originalMarkerRetired"]=false,
            ["inputCounts"]=new JArray(station.Input.Select(value=>value?.count??0)),
            ["outputCounts"]=new JArray(station.Output.Select(value=>value?.count??0)),
            ["queueMultiplier"]=station.Queue?.LastOrDefault()?.Multiplier??0};
        try
        {
            using(var scope=terminal?.Memory?.TryEnterCallbacks()??original.Observation?.Memory?.TryEnterCallbacks()??RebirthProductiveEarnedObservationBudget.TryEnterCandidateDecoder())
            {
                if(scope==null){row["unknown"]=true;return row;}
                if(RebirthProductiveEarnedRowProof.TryDecodeNative(original.Frame,out var before,out _,out _,out _))row["recipient"]=before.StartingEntityId;
                row["originalCurrent"]=original.Artifact?.Current()==true;
                row["nativeMarkerMatches"]=original.Artifact?.MatchesMarker(station.CopyProductiveEarnedMarker())==true;
                if(terminal?.Body!=null)row["terminalBodyDigest"]=Convert.ToBase64String(RebirthProductiveEarnedMobileOrigin.Digest(terminal.Body));
                string parent=original.Artifact?.OriginalPath+".mobile-parent";
                if(parent!=null&&ReadMobileRecord(parent,out var link))
                    using(var stream=new MemoryStream(link,false))using(var reader=new BinaryReader(stream))
                        if(reader.ReadInt32()==0x31504d52)row["parentOperation"]=new Guid(reader.ReadBytes(16)).ToString("N");
            }
        }
        catch{row["unknown"]=true;}
        return row;
    }
    internal static JArray MobileFixtureStatus()
    {
        ObserveMobileAuditWorld();var rows=new JArray();
        foreach(var original in retained.Values.Where(value=>value.Station is RebirthCamperStationState).Take(4))rows.Add(MobileStatusRow(original));
        foreach(var row in mobileReleasedRows.Values)
            if(!rows.OfType<JObject>().Any(value=>(string)value["operation"]==(string)row["operation"]))rows.Add(row.DeepClone());
        return rows;
    }
    internal static JArray MobileHistoricalFixtureStatus(Guid birth,Guid operation)
    {
        var rows=new JArray();
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||birth==Guid.Empty||operation==Guid.Empty)return rows;
            string path=Path.Combine(GameIO.GetSaveGameDir(),"RebirthProductiveEarned",birth.ToString("N"),operation.ToString("N")+".rew");
            if(!ReadMobileRecord(path,out var image)||image.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||!MobileReleasedCurrent(path))return rows;
            var frame=RebirthProductiveEarnedRowProof.Decode(image);
            if(frame.Birth!=birth||frame.Operation!=operation||!frame.HasMobileOrigin||frame.World!=RebirthProductiveEarnedArtifact.WorldIdentity(GameManager.Instance.World)||
                !ReadMobileRecord(path+".mobile-origin",out var origin)||!RebirthProductiveEarnedMobileOrigin.Digest(origin).SequenceEqual(frame.MobileOriginDigest))return rows;
            var source=RebirthProductiveEarnedMobileOrigin.Decode(origin);
            if(source.Installation!=birth||source.Operation!=operation||source.World!=frame.World)return rows;
            var row=new JObject{["operation"]=operation.ToString("N"),["birth"]=birth.ToString("N"),["vehicle"]=source.Vehicle.ToString("N"),
                ["digest"]=Convert.ToBase64String(RebirthProductiveEarnedMobileOrigin.Digest(image)),["fullCountOrigin"]=true,
                ["mobile"]=true,["metadataOnly"]=true,["released"]=true,["held"]=false,["originalMarkerRetired"]=true};
            if(RebirthProductiveEarnedRowProof.TryDecodeNative(frame,out var before,out _,out _,out _))row["recipient"]=before.StartingEntityId;
            rows.Add(row);return rows;
        }
    }
#endif
}
