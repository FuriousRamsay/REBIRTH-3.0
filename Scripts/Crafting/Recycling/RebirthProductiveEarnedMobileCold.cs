using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

internal static partial class RebirthProductiveEarnedCoordinator
{
    private sealed class MobileColdScan
    {
        internal RebirthCookingMillingPublication.NativeMobileLoadSource Source;
        internal IEnumerator<string> Paths;
        internal bool Done,Unknown;
    }
    private static readonly ConditionalWeakTable<RebirthCamperStationState,MobileColdScan> mobileColdScans=
        new ConditionalWeakTable<RebirthCamperStationState,MobileColdScan>();
    private static bool ReadMobileRecord(string path,out byte[] bytes)
        =>RebirthProductiveEarnedMobileOrigin.TryReadRecord(path,out bytes);
    private static byte[] ReadMobileBody(BinaryReader reader)
    {
        int length=reader.ReadInt32();
        if(length<22||length>RebirthProductiveEarnedMobileOrigin.MaximumBytes/2||length>reader.BaseStream.Length-reader.BaseStream.Position)
            throw new InvalidDataException("Mobile source body bound.");
        return reader.ReadBytes(length);
    }
    private static bool ReadMobileTerminalPlan(Attempt original,byte[] bytes,out byte[] body)
    {
        body=null;
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
        {
            if(reader.ReadInt32()!=0x31544d52||!reader.ReadBytes(32).SequenceEqual(original.Artifact.OriginalDigest()))return false;
            original.PendingTimerObserved=reader.ReadBoolean();original.PendingTimerAmbiguous=reader.ReadBoolean();
            original.PendingTimerIndex=reader.ReadInt32();original.PendingTimerBefore=reader.ReadSingle();original.PendingTimerTarget=reader.ReadSingle();
            original.PendingRecipeBinding=reader.ReadString();original.PendingExtras=reader.ReadString();
            if(original.PendingRecipeBinding.Length>RebirthProductiveEarnedRowProof.MaximumFrameBytes||
                original.PendingExtras.Length>RebirthProductiveEarnedRowProof.MaximumFrameBytes||
                float.IsNaN(original.PendingTimerBefore)||float.IsInfinity(original.PendingTimerBefore)||
                float.IsNaN(original.PendingTimerTarget)||float.IsInfinity(original.PendingTimerTarget)||
                original.PendingTimerObserved&&(original.PendingTimerIndex<0||string.IsNullOrEmpty(original.PendingRecipeBinding)||
                    string.IsNullOrEmpty(original.PendingExtras)))return false;
            body=ReadMobileBody(reader);return stream.Position==stream.Length;
        }
    }
    // Entry source is exclusively the real returned native vehicle-load source.
    // A saved descriptor alone never establishes that an entered setter saved.
    private static void TryRestoreMobileTerminal(RebirthCamperStationState station)
    {
        if(station?.BoundVehicle==null||retained.Count>=4||retained.ContainsKey(station))return;
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthCookingMillingPublication.TryObserveNativeLoadedVehicle(station.BoundVehicle,out var source)||
                    !source.TryCopyStationBody(station.InstallationId,out var archived))return;
                var installation=RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(station.InstallationId);
                if(!ReferenceEquals(installation?.State,station))return;
                var saved=RebirthCamperStationSnapshot.Restore(station.InstallationId,installation.InstalledItem,archived);
                var marker=saved.CopyProductiveEarnedMarker();
                if(!station.CopyProductiveEarnedMarker().SequenceEqual(marker))return;
                if(marker.Length!=0)
                {
                    if(!RebirthProductiveEarnedRowProof.TryReadMarker(marker,out var birth,out var operation)||birth!=station.InstallationId)return;
                    TryRestoreMobileTerminalCandidate(station,source,saved,operation,false);
                    return;
                }
                // Marker-absent native cleanup has no operation locator. Walk
                // metadata incrementally; positive released histories use no
                // active root or native graph quota.
                if(!mobileColdScans.TryGetValue(station,out var scan)||!scan.Source.Current())
                {
                    scan?.Paths?.Dispose();mobileColdScans.Remove(station);
                    string directory=Path.Combine(GameIO.GetSaveGameDir(),"RebirthProductiveEarned",station.InstallationId.ToString("N"));
                    if(!RebirthProductiveEarnedArtifact.SafePath(directory)||!Directory.Exists(directory))return;
                    scan=new MobileColdScan{Source=source,Paths=Directory.EnumerateFiles(directory,"*.rew.mobile-clearing").GetEnumerator()};
                    mobileColdScans.Add(station,scan);
                }
                if(scan.Done)return;
                for(int visited=0;visited<4;visited++)
                {
                    if(!scan.Paths.MoveNext()){scan.Done=true;scan.Paths.Dispose();scan.Paths=null;return;}
                    string file=scan.Paths.Current;
                    string original=file.Substring(0,file.Length-".mobile-clearing".Length);
                    if(MobileReleasedCurrent(original))continue;
                    if(Guid.TryParseExact(Path.GetFileNameWithoutExtension(original),"N",out var operation)&&
                        TryRestoreMobileTerminalCandidate(station,source,saved,operation,true))return;
                    scan.Unknown=true;
                }
            }
        }
        catch{/* Unknown original remains held; native load is never an output retry. */}
    }
    private static bool MobileColdScanHeld(RebirthCamperStationState station)
        =>mobileColdScans.TryGetValue(station,out var scan)&&scan.Source.Current()&&(!scan.Done||scan.Unknown);
    private static bool MobileReleasedCurrent(string original)
        =>ReadMobileRecord(original+".mobile-terminal-plan",out var plan)&&ReadMobileRecord(original+".mobile-terminal",out var terminal)&&
            MobileSavedDescriptorCurrent(plan,terminal)&&ReadMobileRecord(original+".mobile-released",out var released)&&
            released.SequenceEqual(RebirthProductiveEarnedMobileOrigin.Digest(terminal));
    private static bool MobileSavedDescriptorCurrent(byte[] plan,byte[] terminal)
    {
        try
        {
            if(terminal.Length<=plan.Length||!terminal.Take(plan.Length).SequenceEqual(plan))return false;
            using(var stream=new MemoryStream(terminal,false))using(var reader=new BinaryReader(stream))
            {
                stream.Position=plan.Length;
                if(reader.ReadString()!=Path.GetFullPath(GameIO.GetSaveGameDir())||reader.ReadInt32()<1||reader.ReadInt32()<1)return false;
                for(int i=0;i<3;i++){string hash=reader.ReadString();if(hash.Length!=44||Convert.FromBase64String(hash).Length!=32)return false;}
                return stream.Position==stream.Length;
            }
        }
        catch{return false;}
    }
    private static bool TryRestoreMobileTerminalCandidate(RebirthCamperStationState station,
        RebirthCookingMillingPublication.NativeMobileLoadSource loaded,RebirthCamperStationState archived,Guid operation,bool clearing)
    {
        MobileTerminal terminal=null;MobileSuccessor successor=null;IDisposable rootRetention=null;
        List<MobileRemoteTools> remoteTools=null;
        MobileRemoteTools pendingRemoteTools=null;
        MobileLocalTools pendingLocalTools=null;
        try
        {
            if(!RebirthProductiveEarnedArtifact.TryReadPreparedIdentity(station,station.InstallationId,operation,out var artifact,out _))return false;
            // A child marker can describe an entered saved successor whose
            // predecessor had not yet finished metadata release.
            if(ReadMobileRecord(artifact.OriginalPath+".mobile-parent",out var link))
            {
                using(var stream=new MemoryStream(link,false))using(var reader=new BinaryReader(stream))
                {
                    if(reader.ReadInt32()!=0x31504d52)return false;
                    Guid parentOperation=new Guid(reader.ReadBytes(16));byte[] parentDigest=reader.ReadBytes(32),recordDigest=reader.ReadBytes(32);
                    if(stream.Position!=stream.Length||!RebirthProductiveEarnedArtifact.TryReadPreparedIdentity(station,station.InstallationId,
                        parentOperation,out var parentArtifact,out _)||!parentArtifact.OriginalDigest().SequenceEqual(parentDigest)||
                        !ReadMobileRecord(parentArtifact.OriginalPath+".mobile-successor",out var parentPlan)||
                        !RebirthProductiveEarnedMobileOrigin.Digest(parentPlan).SequenceEqual(recordDigest))return false;
                    if(!MobileReleasedCurrent(parentArtifact.OriginalPath))artifact=parentArtifact;
                }
            }
            var frame=artifact.ReadFrame();
            if(!frame.HasMobileOrigin||!RebirthProductiveEarnedMobileOrigin.TryRead(artifact,frame,out var origin)||
                origin.Vehicle!=RebirthCamperModuleRuntime.For(station.BoundVehicle).Identity||
                !ReadMobileRecord(artifact.OriginalPath+".mobile-terminal-plan",out var plan)||
                !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(artifact.OriginalPath+".mobile-output-invoking",artifact.OriginalDigest()))return false;
            var value=new Attempt{Station=station,Artifact=artifact,Frame=frame,Recipe=station.Queue?.LastOrDefault()?.Recipe,
                Row=station.Queue?.LastOrDefault(),Multiplier=frame.Multiplier,OutputTimerObserved=true,MobileOutputReturned=true};
            if(!ReadMobileTerminalPlan(value,plan,out var before))return false;
            var installed=RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(station.InstallationId).InstalledItem;
            if(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(new[]{installed},196608,262144,out var installedImage)||
                installedImage!=origin.InstalledItem)return false;
            byte[] body=RebirthProductiveEarnedMobileOrigin.Canonical(archived,installed);
            byte[] positiveRecord=null;
            if(ReadMobileRecord(artifact.OriginalPath+".mobile-terminal",out var savedTerminal))
            {if(!MobileSavedDescriptorCurrent(plan,savedTerminal))return false;positiveRecord=savedTerminal;}
            else if(File.Exists(artifact.OriginalPath+".mobile-terminal.tmp"))return false;
            if(positiveRecord==null&&(File.Exists(artifact.OriginalPath+".mobile-tools-plan")||
                File.Exists(artifact.OriginalPath+".tool-gesture")))return false;
            if(!TryReadMobileRemoteTools(value,installed,before,body,out var remoteAfter,out remoteTools,out pendingRemoteTools))return false;
            before=remoteAfter;
            if(!TryReadMobileToolsSource(value,installed,before,body,out var toolsAfter,out var toolRecord,out pendingLocalTools))return false;
            before=toolsAfter;
            if(ReadMobileRecord(artifact.OriginalPath+".mobile-successor",out var successorRecord))
            {
                successor=ReadMobileSuccessor(value,successorRecord,before);
                if(successor==null)return false;
                string entered=artifact.OriginalPath+".mobile-successor.invoking";
                if(ReadMobileRecord(entered,out var invoking))
                {
                    if(!invoking.SequenceEqual(successorRecord)||!body.SequenceEqual(successor.After))return false;
                    successor.Entered=successor.Returned=true;
                    byte[] expected=successor.Next?.Artifact.Marker(successor.Next.Frame)??artifact.Marker(frame);
                    if(!clearing&&!archived.CopyProductiveEarnedMarker().SequenceEqual(expected))return false;
                }
                else
                {
                    if(File.Exists(entered+".tmp")||!body.SequenceEqual(before)||
                        !artifact.MatchesMarker(archived.CopyProductiveEarnedMarker()))return false;
                }
            }
            else if(!body.SequenceEqual(before)||!clearing&&!artifact.MatchesMarker(archived.CopyProductiveEarnedMarker()))return false;
            if(!RebirthProductiveEarnedMobileOrigin.Canonical(station,installed).SequenceEqual(body)||!loaded.OriginalCurrent())return false;
            artifact.observedRemoteIdentity=()=>loaded.Current()&&ReferenceEquals(station.BoundVehicle,loaded.Vehicle)&&
                RebirthCamperModuleRuntime.For(station.BoundVehicle).Identity==origin.Vehicle;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*(body.Length+plan.Length)+4L*artifact.ImageLength+1048576+
                (successor?.Next==null?0:4L*RebirthStationSnapshotEvidence.MaximumSnapshotBytes+
                    2L*(RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace/3))),out rootRetention))return false;
            terminal=new MobileTerminal{Original=value,Station=station,Installed=installed,Body=body,Plan=plan,ToolRecord=toolRecord,
                PendingLocalTools=pendingLocalTools,PendingRemoteTools=pendingRemoteTools};
            pendingRemoteTools=null;pendingLocalTools=null;
            terminal.RemoteTools.AddRange(remoteTools);
            remoteTools=null;
            if(positiveRecord!=null)
            {
                // Positive terminal is the exact original plan followed by the
                // real saved vehicle descriptor. No shape-based replacement.
                terminal.Record=positiveRecord;terminal.Positive=true;
            }
            else if(successor?.Entered==true||clearing)return false;
            if(clearing)
            {
                if(!ReadMobileRecord(artifact.OriginalPath+".mobile-clearing",out var clear)||
                    !clear.SequenceEqual(RebirthProductiveEarnedMobileOrigin.Digest(terminal.Record))||
                    archived.CopyProductiveEarnedMarker().Length!=0)return false;
                terminal.Clearing=true;value.PendingTimerObserved=false;
                if(successor!=null){successor.Dispose();successor=null;}
            }
            value.Retention=rootRetention;rootRetention=null;value.MobileTerminal=terminal;value.MobileSuccessor=successor;
            // Exit the decoder before opening the persistent native observation.
            retained.Add(station,value);terminal=null;successor=null;
            return true;
        }
        catch{return false;}
        finally{terminal?.Dispose();successor?.Dispose();rootRetention?.Dispose();pendingRemoteTools?.Dispose();pendingLocalTools?.Dispose();if(remoteTools!=null)foreach(var source in remoteTools)source.Dispose();}
    }
    private static MobileSuccessor ReadMobileSuccessor(Attempt parent,byte[] record,byte[] terminalBefore)
    {
        using(var stream=new MemoryStream(record,false))using(var reader=new BinaryReader(stream))
        {
            if(reader.ReadInt32()!=0x31534d52||!reader.ReadBytes(32).SequenceEqual(parent.Artifact.OriginalDigest()))return null;
            bool child=reader.ReadBoolean();Guid childId=new Guid(reader.ReadBytes(16));byte[] childDigest=reader.ReadBytes(32);
            float target=reader.ReadSingle();string binding=reader.ReadString(),extras=reader.ReadString();
            if(float.IsNaN(target)||float.IsInfinity(target)||binding!=parent.PendingRecipeBinding||extras!=parent.PendingExtras)return null;
            byte[] before=ReadMobileBody(reader),after=ReadMobileBody(reader);
            if(stream.Position!=stream.Length||!before.SequenceEqual(terminalBefore)||!parent.PendingTimerObserved)return null;
            var station=(RebirthCamperStationState)parent.Station;
            var installed=RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(station.InstallationId).InstalledItem;
            var detached=RebirthCamperStationSnapshot.Restore(station.InstallationId,installed,before);
            int index=parent.PendingTimerIndex;
            if(index<0||index!=detached.Queue.Length-1)return null;
            var row=detached.Queue[index];float work,expected;
            byte[] frozenBeforeRow=RebirthProductiveEarnedRowProof.CaptureRow(row);var frozenBeforeTools=detached.Tools;
            if(row?.Recipe==null||row.CraftingTimeLeft!=parent.PendingTimerBefore||
                RebirthProductiveEarnedUiQueueOwner.Binding(row)!=binding||RebirthRecipeOutputs.DefinitionIdentity(row.Recipe)!=extras||
                !(child?TryEarnedNativeChildWork(parent.PendingTimerBefore,parent.PendingTimerTarget,row.OneItemCraftTime,out work,out expected):
                    TryPartialNativeWork(parent.PendingTimerBefore,parent.PendingTimerTarget,row.OneItemCraftTime,out work,out expected))||
                expected!=target||RebirthProductiveToolWork.Limit(detached.Tools,row.Recipe,detached.block,work)!=work)return null;
            var tools=RebirthProductiveToolWork.Wear(detached.Tools,row.Recipe,detached.block,work);
            detached.Tools=tools;row.CraftingTimeLeft=target;
            if(!RebirthProductiveEarnedMobileOrigin.Canonical(detached,installed).SequenceEqual(after))return null;
            var result=new MobileSuccessor{Parent=parent,Before=before,After=after,Record=record,Tools=tools,Target=target,
                Row=station.Queue[index]};
            if(child)
            {
                if(childId==Guid.Empty||!RebirthProductiveEarnedArtifact.TryReadPreparedIdentity(station,station.InstallationId,childId,out var artifact,out _)||
                    !artifact.OriginalDigest().SequenceEqual(childDigest))return null;
                var frame=artifact.ReadFrame();
                if(!frame.HasMobileOrigin||!RebirthProductiveEarnedMobileOrigin.TryRead(artifact,frame,out var origin)||
                    !origin.Before.SequenceEqual(before)||!origin.After.SequenceEqual(after)||frame.WorkedSeconds!=work||
                    origin.Vehicle!=RebirthCamperModuleRuntime.For(station.BoundVehicle).Identity||frame.RowIndex!=index||
                    frame.FullRecipeBinding!=binding||frame.ExtrasDefinition!=extras||
                    !frame.BeforeRow.SequenceEqual(frozenBeforeRow)||!frame.AfterRow.SequenceEqual(RebirthProductiveEarnedRowProof.CaptureRow(row))||
                    !RebirthProductiveEarnedArtifact.SameTools(frozenBeforeTools,frame.BeforeTools)||!RebirthProductiveEarnedArtifact.SameTools(tools,frame.AfterTools)||
                    !RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(new[]{installed},196608,262144,out var installedImage)||origin.InstalledItem!=installedImage)return null;
                result.Next=new Attempt{Station=station,Artifact=artifact,Frame=frame,Row=station.Queue[index],Recipe=station.Queue[index].Recipe,
                    Multiplier=frame.Multiplier,Target=target,Tools=tools};
            }
            else if(childId!=Guid.Empty||childDigest.Any(b=>b!=0))return null;
            return result;
        }
    }
}
