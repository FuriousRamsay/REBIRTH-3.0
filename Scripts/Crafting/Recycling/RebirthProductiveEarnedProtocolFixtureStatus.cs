#if REBIRTH_DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

// Read-only phase assertions for future independent-peer qualification. No
// fault injection, packet replay, phase advancement or fixture payment authority.
public sealed class ConsoleCmdRebirthEarnedProtocolFixtureStatus:ConsoleCmdAbstract
{
    public override string[] getCommands()=>new[]{"rbearnedstatus"};
    public override string getDescription()=>"Read actual original earned protocol metadata in disposable CodexTest.";
    public override string getHelp()=>"rbearnedstatus [ui|native|mobile|initial|viewerTools] [expect opGuid field true|false] (actual local CodexTest Bridge only).";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        if(sender.RemoteClientInfo!=null||!ThreadManager.IsMainThread()||!RebirthGameBridge.IsRunning||
            !RebirthProvenanceDetachedReservation.IsDisposableSave()||GameManager.Instance?.World==null)
        {SdtdConsole.Instance.Output("*** ERROR "+getHelp());return;}
        try
        {
            bool remote=GameManager.Instance.World.IsRemote();
            bool mobile=args.Count>0&&args[0]=="mobile";
            bool ui=args.Count>0&&args[0]=="ui",native=args.Count>0&&args[0]=="native",initial=args.Count>0&&args[0]=="initial",viewerTools=args.Count>0&&args[0]=="viewerTools";if(ui||native||mobile||initial||viewerTools)args=new List<string>(args.Skip(1));
            var rows=viewerTools?(remote?RebirthProductiveEarnedRemoteClient.ViewerToolsFixtureStatus():RebirthProductiveEarnedRemoteHost.ViewerToolsFixtureStatus()):initial?RebirthProductiveEarnedUiQueueOwner.InitialFixtureStatus():ui?RebirthProductiveEarnedUiCoordinator.FixtureStatus():native?RebirthProductiveEarnedCoordinator.FixtureStatus():remote?RebirthProductiveEarnedRemoteClient.FixtureStatus():RebirthProductiveEarnedRemoteHost.FixtureStatus();
            if(mobile)rows=RebirthProductiveEarnedCoordinator.MobileFixtureStatus();
            if(mobile&&args.Count==3&&args[0]=="history"&&Guid.TryParseExact(args[1],"N",out var mobileBirth)&&
                Guid.TryParseExact(args[2],"N",out var mobileOperation))
            {rows=RebirthProductiveEarnedCoordinator.MobileHistoricalFixtureStatus(mobileBirth,mobileOperation);args.Clear();}
            var result=new JObject{["schema"]=1,["role"]=viewerTools?(remote?"viewer-tools-owner":"viewer-tools-host"):initial?"initial-queue":ui?"owning-ui":native?"native-loop":remote?"owner":"host",["enabled"]=initial?RebirthProductiveEarnedUiQueueOwner.InitialObserverInstalled:ui?RebirthProductiveEarnedUiOriginal.Enabled:RebirthProductiveEarnedCoordinator.Enabled,["readOnly"]=true,["originals"]=rows};
            if(mobile)result["role"]="mobile-native";
            if(args.Count!=0)
            {
                if(args.Count!=4||args[0]!="expect"||!Guid.TryParseExact(args[1],"N",out var operation)||
                    !bool.TryParse(args[3],out bool expected))throw new InvalidDataException(getHelp());
                var matches=rows.OfType<JObject>().Where(row=>(string)row["operation"]==operation.ToString("N")).ToArray();
                if(matches.Length!=1||matches[0][args[2]]?.Type!=JTokenType.Boolean||(bool)matches[0][args[2]]!=expected)
                    throw new InvalidDataException("Exact actual original phase assertion failed");
                result["asserted"]=args[2];result["passed"]=true;
            }
            SdtdConsole.Instance.Output(result.ToString(Newtonsoft.Json.Formatting.None));
        }
        catch(Exception exception){SdtdConsole.Instance.Output("*** ERROR "+exception.Message);}
    }
}
internal static partial class RebirthProductiveEarnedCoordinator
{
    internal static JArray FixtureStatus()
    {
        var result=new JArray();
        foreach(var original in retained.Values.Take(4))
        {
            var row=new JObject{["operation"]=original.Frame.Operation.ToString("N"),["birth"]=original.Frame.Birth.ToString("N"),
                ["custodyBirth"]=original.Artifact?.CustodyBirth.ToString("N"),["multiplier"]=original.Multiplier,
                ["outputTimerObserved"]=original.OutputTimerObserved,["returnedTerminal"]=original.Terminal!=null,
                ["pendingNextWork"]=original.Terminal?.PendingNextWork==true,
                ["toolRebased"]=original.Terminal?.ToolRebaseCurrent()==true,
                ["loadedToolRebase"]=original.Terminal?.SavedOrigin?.ToolsRebased==true,
                ["digest"]=original.Artifact==null?null:Convert.ToBase64String(original.Artifact.OriginalDigest()),
                ["loadedSource"]=original.Artifact?.LoadedSource!=null};
            try{row["originalCurrent"]=original.Artifact?.Current()==true;row["earnedSaved"]=original.Observation?.Saved()==true;
                row["terminalCurrent"]=original.Terminal?.Current()==true;
                row["terminalSaved"]=original.Terminal?.Watch?.TryReadPublishedTileDescriptor(out _,out _,out _,out _)==true;}
            catch{row["unknown"]=true;}result.Add(row);
        }
        return result;
    }
}
internal static partial class RebirthProductiveEarnedUiCoordinator
{
    internal static JArray FixtureStatus()
    {
        var rows=new JArray();
        foreach(var original in originals.Values.Take(4))
        {
            var row=new JObject{["operation"]=original.Binding.Operation.ToString("N"),["birth"]=original.Binding.Birth.ToString("N"),
                ["nonce"]=original.Nonce.ToString("N"),["digest"]=original.Digest,["preparationComplete"]=original.PreparationComplete,
                ["ownerBeforeAttached"]=original.OwnerBeforeAttached,["wearAttempted"]=original.WearAttempted,["wearObserved"]=original.WearObserved,
                ["worn"]=original.Worn,["earnedSaved"]=original.EarnedSaved,["completionAttempted"]=original.CompletionAttempted,
                ["delivered"]=original.Delivered,["viewerSaved"]=original.ViewerSaved,["stationPublished"]=original.StationPublished,
                ["terminalSaved"]=original.TerminalSaved,["preparationPlan"]=original.PreparationTools==null?null:Hash(original.PreparationTools)};
            row["beforeEntered"]=original.BeforeFlush?.Entered==true;row["earnedEntered"]=original.EarnedFlush?.Entered==true;
            try{row["current"]=original.Current();row["beforeObserved"]=original.BeforeFlush?.IsObserved==true;
                row["earnedObserved"]=original.EarnedFlush?.IsObserved==true;}catch{row["unknown"]=true;}
            rows.Add(row);
        }
        return rows;
    }
}
internal static partial class RebirthProductiveEarnedRemoteClient
{
    internal static JArray FixtureStatus()
    {
        var result=new JArray();
        foreach(var original in originals.Values.Take(4))
        {
            var row=new JObject{["operation"]=original.Frame.Operation.ToString("N"),["birth"]=original.Frame.Birth.ToString("N"),
                ["nonce"]=original.Nonce.ToString("N"),["digest"]=original.Digest,["deaths"]=original.Deaths,["recovered"]=original.Recovered};
            try
            {
                row["current"]=original.Current();row["before"]=RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,original.Frame.BeforeNative,original.Frame.BeforeElapsedOffset);
                row["after"]=RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,original.Frame.AfterNative,original.Frame.AfterElapsedOffset);
                row["nativeOutputAttempted"]=original.Output?.Attempted==true;row["nativeOutputReturned"]=original.Output?.Returned==true;
                row["nativeOutputSaved"]=original.Output?.Saved==true;row["nativeOutputCurrent"]=original.Output?.Returned==true&&CurrentNativeOutputOwner(original);
                row["nativePendingPlanned"]=original.Pending!=null;row["nativePendingReturned"]=original.Pending?.Returned==true;row["nativePendingSaved"]=original.Pending?.Saved==true;
                row["nativeViewerToolsOnly"]=original.ViewerToolsOnly;row["nativeViewerToolsReleased"]=SavedNativeCloseStage(original.Path,new RebirthProductiveEarnedRemoteClosedRecord.Description{Operation=original.Frame.Operation,Nonce=original.Nonce,OriginalDigest=original.Digest},"nativeViewerReleased");row["nativeViewerToolsHeld"]=original.ViewerToolsOnly;
                row["nativeToolsPlanned"]=original.Tools!=null;row["nativeToolsSteps"]=(original.Tools?.Steps??0)>0;row["nativeToolsHeader"]=original.Tools?.Header==true;
                row["nativeToolsOwnerSaved"]=original.Tools?.OwnerSaved==true;row["nativeToolsSaveRequested"]=original.Tools?.SaveRequested==true;
                row["nativeToolsSyncEntered"]=original.Tools?.SyncEntered==true;row["nativeToolsSyncReturned"]=original.Tools?.SyncReturned==true;row["nativeToolsSaved"]=original.Tools?.Saved==true;
                row["closeRequested"]=false;row["closed"]=false;row["released"]=false;row["nativeHeld"]=true;
                foreach(string stage in new[]{"accepted","beforeUpload","attempt","applied","afterUpload","published"})row[stage]=MatchesStage(original,stage,original.Nonce);
            }
            catch{row["unknown"]=true;}
            result.Add(row);
        }
        AppendNativeClosedFixtureStatus(result);return result;
    }
}
internal static partial class RebirthProductiveEarnedRemoteHost
{
    internal static JArray FixtureStatus()
    {
        var result=new JArray();
        foreach(var original in pending.Values.Take(4))
        {
            var row=new JObject{["operation"]=original.Operation.ToString("N"),["birth"]=original.Artifact.Birth.ToString("N"),
                ["nonce"]=original.Nonce.ToString("N"),["digest"]=Digest(original.Image),["deaths"]=original.Deaths};
            try
            {
                row["current"]=original.Current();row["before"]=original.BeforeCurrent();row["after"]=original.AfterCurrent();
                row["beforeAccepted"]=original.BeforeAccepted;row["afterAccepted"]=original.Accepted;
                row["nativeOutputPlanned"]=original.NativeOutput!=null;row["nativeOutputAccepted"]=original.NativeOutputAccepted;
                row["nativeOutputSaved"]=original.NativeOutputSaved;row["nativeOutputCurrent"]=original.NativeOutput!=null&&NativeOutputCurrent(original);
                row["nativeOutputLoaded"]=original.Artifact.LoadedSource?.Terminal==true;
                row["nativePendingPlanned"]=original.NativePending!=null;row["nativePendingAccepted"]=original.NativePendingAccepted;row["nativePendingSaved"]=original.NativePendingSaved;
                row["nativeViewerToolsOnly"]=original.ViewerToolsOnly;row["nativeViewerToolsReleased"]=original.ViewerToolsReleased;row["nativeViewerToolsHeld"]=original.ViewerToolsOnly&&(NativeViewerToolsHeld(original.Artifact.OriginalPath)||NativeViewerToolsRuntimeHeld(original.Station));
                row["nativeToolsPlanned"]=original.Tools!=null;row["nativeToolsOwnerAccepted"]=original.Tools?.OwnerAccepted==true;row["nativeToolsOwnerWritten"]=original.Tools?.OwnerWritten==true;
                row["nativeToolsOwnerSaved"]=original.Tools?.OwnerSaved==true;row["nativeToolsAccepted"]=original.Tools?.Accepted==true;row["nativeToolsSaved"]=original.Tools?.Saved==true;
                row["closeRequested"]=false;row["closed"]=false;row["released"]=false;row["nativeHeld"]=true;
                string path=original.HeaderPath+".beforeSaved";
                row["beforeSaved"]=original.Current()&&Safe(path)&&File.Exists(path)&&!File.Exists(path+".tmp")&&ValidateBeforeReceipt(original,ReadBeforeReceipt(path));
                string afterPath=original.HeaderPath+".afterSaved";
                row["afterSaved"]=original.AfterPublished&&original.Current()&&original.AfterCurrent()&&File.Exists(afterPath)&&
                    !File.Exists(afterPath+".tmp")&&ValidateAfterReceipt(original,ReadBeforeReceipt(afterPath));
            }
            catch{row["unknown"]=true;}
            result.Add(row);
        }
        AppendNativeClosedFixtureStatus(result);return result;
    }
}
#endif
