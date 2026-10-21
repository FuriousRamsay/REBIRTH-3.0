using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

internal static partial class RebirthProductiveEarnedCoordinator
{
    internal sealed class MobileRemoteTools:IDisposable
    {
        internal Guid Operation;
        internal string Path;
        internal byte[] Record,Before,After,TransferBefore,TransferAfter,OwnerDigest;
        internal byte[] RecordHash,BeforeHash,AfterHash;
        internal long RecordLength;
        internal bool Reentry,Entered,ProjectionReady;
        internal RebirthCamperStationSaveEvidence.Request Save;
        internal RebirthCamperStationSaveEvidence.Publication Saved;
        internal string Account,Creation;
        internal IDisposable Retention;
        internal bool Current()
        {
            try
            {
                byte[] digest=RecordHash??RebirthProductiveEarnedMobileOrigin.Digest(Record);
                return (Record==null?MobileRemoteRecordCurrent(this):RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(Path,Record,RebirthProductiveEarnedMobileOrigin.MaximumBytes))&&
                    RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(Path+".saved",digest)&&
                    MobileRemoteOwnerCurrent(this);
            }
            catch{return false;}
        }
        internal bool Compact()
        {
            if(Record==null)return true;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4096L+2L*(Path.Length+Account.Length+Creation.Length)),out var compact))return false;
            RecordHash=RebirthProductiveEarnedMobileOrigin.Digest(Record);RecordLength=Record.Length;
            BeforeHash=RebirthProductiveEarnedMobileOrigin.Digest(Before);AfterHash=RebirthProductiveEarnedMobileOrigin.Digest(After);
            Record=Before=After=null;Retention?.Dispose();Retention=compact;return true;
        }
        public void Dispose(){Save?.Dispose();Saved?.Dispose();Save=null;Saved=null;Retention?.Dispose();Retention=null;Record=Before=After=TransferBefore=TransferAfter=null;}
    }
    private static bool mobileRemoteToolsInstalled;
    internal static void InstallMobileToolSourceObserver()
    {
        if(mobileRemoteToolsInstalled)return;
        RebirthCamperTransferJournal.PublicationFinished+=ObserveMobileToolTransfer;
        mobileRemoteToolsInstalled=true;
    }
    internal static bool CanPrepareMobileRemoteTools(RebirthCamperStationState station,RebirthCamperRemoteStationGrid kind)
    {
        if(kind!=RebirthCamperRemoteStationGrid.Tools||!Enabled||station==null||!retained.TryGetValue(station,out var original))return false;
        if(RebirthCamperStationWindowSession.HasOriginalProjection(station)||RebirthCamperStationSaveEvidence.IsPending(station)||
            RebirthCamperStationCompletionCallsite.IsHeld(station)||RebirthCamperStationCancellationRemoval.IsHeld(station)||
            RebirthCamperStationNativeActorPayment.IsHeld(station)||RebirthCamperStationFuelOriginal.IsHeld(station)||
            RebirthCookingMillingPublication.Held(station)||RebirthRemoteResourceFetchedStationOriginal.BlocksStation(station))return false;
        return original.MobileTerminal?.Positive==true&&!original.MobileTerminal.Clearing&&!original.PendingTimerAmbiguous&&
            original.PendingTimerObserved&&original.MobileSuccessor==null&&original.MobileTerminal.PendingLocalTools==null&&
            original.MobileTerminal.PendingRemoteTools==null&&original.MobileTerminal.ToolRecord==null&&original.MobileTerminal.Current();
    }
    internal static bool PrepareMobileRemoteTools(EntityVehicle vehicle,Guid installation,Guid operation,byte[] before,byte[] after,
        string account,string creation,Func<bool> current)
    {
        if(!Enabled)return true;
        MobileRemoteTools value=null;IDisposable ownerMemory=null;
        try
        {
            var station=RebirthCamperModuleRuntime.For(vehicle).Stations.Find(installation)?.State;
            if(station==null||!retained.TryGetValue(station,out var original))return true;
            var terminal=original.MobileTerminal;
            if(terminal?.PendingRemoteTools!=null)
            {
                var pending=terminal.PendingRemoteTools;
                if(pending.Operation!=operation||pending.Account!=account||pending.Creation!=creation||current?.Invoke()!=true||
                    !MobileRemoteOwnerCurrent(pending)||!original.Artifact.Current()||
                    !pending.TransferBefore.SequenceEqual(RebirthProductiveEarnedMobileOrigin.Digest(before))||
                    !pending.TransferAfter.SequenceEqual(RebirthProductiveEarnedMobileOrigin.Digest(after))||
                    !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(pending.Path,pending.Record,RebirthProductiveEarnedMobileOrigin.MaximumBytes))return false;
                byte[] currentBody=RebirthProductiveEarnedMobileOrigin.Canonical(station,terminal.Installed);
                return currentBody.SequenceEqual(pending.Before)||currentBody.SequenceEqual(pending.After);
            }
            if(!CanPrepareMobileRemoteTools(station,RebirthCamperRemoteStationGrid.Tools))return false;
            if(terminal.RemoteTools.Count>=64||terminal.ToolRecord!=null)return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return false;
                var runtime=RebirthCamperModuleRuntime.For(vehicle);
                var prior=RebirthCamperTransferSnapshot.Restore(runtime.Identity,before);
                var next=RebirthCamperTransferSnapshot.Restore(runtime.Identity,after);
                var old=prior.Stations.Find(installation);var replacement=next.Stations.Find(installation);
                if(old==null||replacement==null||!original.Artifact.MatchesMarker(old.State.CopyProductiveEarnedMarker())||
                    !original.Artifact.MatchesMarker(replacement.State.CopyProductiveEarnedMarker()))return false;
                byte[] source=RebirthProductiveEarnedMobileOrigin.Canonical(old.State,old.InstalledItem);
                byte[] target=RebirthProductiveEarnedMobileOrigin.Canonical(replacement.State,replacement.InstalledItem);
                if(!source.SequenceEqual(terminal.Body)||!ValidMobileToolsOnly(installation,old.InstalledItem,source,target))return false;
                value=new MobileRemoteTools{Operation=operation,Before=source,After=target,
                    TransferBefore=RebirthProductiveEarnedMobileOrigin.Digest(before),TransferAfter=RebirthProductiveEarnedMobileOrigin.Digest(after),
                    Path=original.Artifact.OriginalPath+".mobile-remote-tools."+operation.ToString("N"),Account=account,Creation=creation};
                byte[] ownerImage;
                if(File.Exists(value.Path+".owner"))
                {
                    long length=new FileInfo(value.Path+".owner").Length;
                    if(length<5||length>16*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(64L*1024*1024+4*length),out ownerMemory)||
                        !ReadMobileRecord(value.Path+".owner",out ownerImage)||!RebirthCamperSavedCursor.VerifyPublishedOwnerImage(ownerImage,operation))return false;
                }
                else if(!RebirthCamperSavedCursor.TryCopyPublishedOwner(account,operation,current,out ownerImage,out ownerMemory))return false;
                value.OwnerDigest=RebirthProductiveEarnedMobileOrigin.Digest(ownerImage);
                if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(value.Path+".owner",ownerImage,16*1024*1024))return false;
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {
                    writer.Write(0x32524d52);writer.Write(original.Artifact.OriginalDigest());writer.Write(operation.ToByteArray());
                    writer.Write(runtime.Identity.ToByteArray());writer.Write(installation.ToByteArray());
                    writer.Write(account);writer.Write(creation);writer.Write(value.OwnerDigest);
                    writer.Write(value.TransferBefore);writer.Write(value.TransferAfter);
                    writer.Write(source.Length);writer.Write(source);writer.Write(target.Length);writer.Write(target);
                    writer.Flush();value.Record=stream.ToArray();
                }
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*value.Record.Length+4L*(source.Length+target.Length)+65536),out value.Retention)||
                    !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(value.Path,value.Record,RebirthProductiveEarnedMobileOrigin.MaximumBytes))return false;
                if(terminal.PendingRemoteTools!=null)
                {
                    if(terminal.PendingRemoteTools.Operation!=operation||!terminal.PendingRemoteTools.Record.SequenceEqual(value.Record))return false;
                    return true;
                }
                terminal.PendingRemoteTools=value;value=null;return true;
            }
        }
        catch{return false;}
        finally{value?.Dispose();ownerMemory?.Dispose();}
    }
    internal static bool PrepareMobileRemoteToolReceipt(EntityVehicle vehicle,RebirthCamperTransferJournal.Record receipt,
        string account,string creation,Func<bool> current)
    {
        if(!Enabled)return true;
        try
        {
            if(vehicle==null||receipt==null||receipt.Vehicle!=RebirthCamperModuleRuntime.For(vehicle).Identity||
                string.IsNullOrEmpty(account)||string.IsNullOrEmpty(creation)||receipt.OwnerCreation!="remote:"+account+":"+creation||current?.Invoke()!=true)return false;
            foreach(var original in retained.Values.ToArray())
            {
                if(original.Station is not RebirthCamperStationState station||!ReferenceEquals(station.BoundVehicle,vehicle))continue;
                var before=RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.Before).Stations.Find(station.InstallationId);
                var after=RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.After).Stations.Find(station.InstallationId);
                if(before==null||after==null)return false;
                if(RebirthProductiveEarnedMobileOrigin.Canonical(before.State,before.InstalledItem).SequenceEqual(
                    RebirthProductiveEarnedMobileOrigin.Canonical(after.State,after.InstalledItem)))continue;
                if(!PrepareMobileRemoteTools(vehicle,station.InstallationId,receipt.Operation,receipt.Before,receipt.After,account,creation,current))return false;
            }
            return true;
        }
        catch{return false;}
    }
    private static void ObserveMobileToolTransfer(string directory,RebirthCamperTransferJournal.Record receipt,bool published)
    {
        if(!published||receipt?.State!=RebirthCamperTransferJournal.Phase.Applied||!Enabled||!ThreadManager.IsMainThread()||
            Path.GetFullPath(directory)!=Path.GetFullPath(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CamperTransfers")))return;
        foreach(var original in retained.Values.ToArray())
        {
            var terminal=original.MobileTerminal;var source=terminal?.PendingRemoteTools;
            if(source?.Operation!=receipt.Operation)continue;
            try
            {
                TryAdoptMobileRemoteTools(original,source,receipt);
            }
            catch{/* Native receipt remains exact; a cold actual load can finish metadata. */}
        }
    }
    private static bool TryAdoptMobileRemoteTools(Attempt original,MobileRemoteTools source,RebirthCamperTransferJournal.Record receipt)
    {
        var terminal=original.MobileTerminal;
        if(receipt?.State!=RebirthCamperTransferJournal.Phase.Applied||
            !MatchesMobileRemoteToolReceipt(source,receipt,RebirthCamperModuleRuntime.For(terminal.Station.BoundVehicle).Identity)||!MobileRemoteOwnerCurrent(source)||
            !original.Artifact.Current()||!original.Artifact.MatchesMarker(terminal.Station.CopyProductiveEarnedMarker())||
            !RebirthProductiveEarnedMobileOrigin.Canonical(terminal.Station,terminal.Installed).SequenceEqual(source.After)||
            !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(source.Path+".saved",RebirthProductiveEarnedMobileOrigin.Digest(source.Record)))return false;
        // Applied is the existing endpoint's positive saved owner+vehicle
        // terminal. Event and retry reads use the same immutable exact receipt.
        terminal.Body=(byte[])source.After.Clone();terminal.RemoteTools.Add(source);terminal.PendingRemoteTools=null;
        source.Compact();return true;
    }
    private static bool RetryMobileRemoteTools(Attempt original)
    {
        var source=original.MobileTerminal?.PendingRemoteTools;if(source==null)return true;
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CamperTransfers");
        var receipt=RebirthCamperTransferJournal.ReadExisting(directory,source.Operation);
        if(!TryAdoptMobileRemoteTools(original,source,receipt))return false;
        var runtime=RebirthCamperModuleRuntime.For(((RebirthCamperStationState)original.Station).BoundVehicle);
        if(runtime.TransferPending)
        {
            new RebirthCamperTransferJournal(directory).AssertAvailable(runtime.Identity,source.Operation);
            runtime.SetTransferPending(false);runtime.Persist(true,12);
        }
        return true;
    }
    private static bool TryReadMobileRemoteTools(Attempt original,ItemStack installed,byte[] before,byte[] loadedBody,
        out byte[] after,out List<MobileRemoteTools> sources,out MobileRemoteTools pending)
    {
        after=before;pending=null;sources=new List<MobileRemoteTools>();var candidates=new List<MobileRemoteTools>();bool accepted=false;
        try
        {
            string prefix=original.Artifact.OriginalPath+".mobile-remote-tools.";
            foreach(string path in Directory.EnumerateFiles(Path.GetDirectoryName(prefix),Path.GetFileName(prefix)+"*"))
            {
                if(!TryClassifyMobileRemoteToolFile(prefix,path,out var fileOperation,out bool companion))return false;
                if(companion)
                {
                    string primary=prefix+fileOperation.ToString("N");
                    if(!File.Exists(primary)||File.Exists(primary+".tmp"))return false;
                    continue;
                }
                if(candidates.Count>=64||path.EndsWith(".tmp",StringComparison.Ordinal)||!File.Exists(path)||!File.Exists(path+".owner"))return false;
                long size=new FileInfo(path).Length,ownerSize=new FileInfo(path+".owner").Length;
                if(size<1||size>RebirthProductiveEarnedMobileOrigin.MaximumBytes||ownerSize<5||ownerSize>16*1024*1024||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(64L*1024*1024+8*size+4*ownerSize),out var workspace))return false;
                using(workspace)
                {
                if(!ReadMobileRecord(path,out var record))return false;
                using(var stream=new MemoryStream(record,false))using(var reader=new BinaryReader(stream))
                {
                    if(reader.ReadInt32()!=0x32524d52||!reader.ReadBytes(32).SequenceEqual(original.Artifact.OriginalDigest()))return false;
                    var value=new MobileRemoteTools{Operation=new Guid(reader.ReadBytes(16)),Path=path,Record=record};
                    Guid vehicle=new Guid(reader.ReadBytes(16)),installation=new Guid(reader.ReadBytes(16));
                    value.Account=reader.ReadString();value.Creation=reader.ReadString();value.OwnerDigest=reader.ReadBytes(32);
                    value.TransferBefore=reader.ReadBytes(32);value.TransferAfter=reader.ReadBytes(32);
                    value.Before=ReadMobileBody(reader);value.After=ReadMobileBody(reader);
                    if(stream.Position!=stream.Length||installation!=original.Artifact.Birth||value.Operation==Guid.Empty||
                        path!=prefix+value.Operation.ToString("N")||vehicle!=RebirthCamperModuleRuntime.For(((RebirthCamperStationState)original.Station).BoundVehicle).Identity||
                        !ValidMobileToolsOnly(installation,installed,value.Before,value.After))return false;
                    byte[] originalDigest=RebirthProductiveEarnedMobileOrigin.Digest(value.Record);
                    bool reentry=File.Exists(path+".reentry"),invoking=File.Exists(path+".reentry.invoking");
                    if(File.Exists(path+".reentry.tmp")||File.Exists(path+".reentry.invoking.tmp")||
                        invoking&&!reentry||reentry&&!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(path+".reentry",originalDigest)||
                        invoking&&!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(path+".reentry.invoking",originalDigest))return false;
                    var receipt=RebirthCamperTransferJournal.ReadExisting(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CamperTransfers"),value.Operation);
                    if(receipt==null||receipt.Vehicle!=vehicle||receipt.State!=RebirthCamperTransferJournal.Phase.Applied&&
                        receipt.State!=RebirthCamperTransferJournal.Phase.Uncertain||
                        receipt.OwnerCreation!="remote:"+value.Account+":"+value.Creation||!MobileRemoteOwnerCurrent(value)||
                        !RebirthProductiveEarnedMobileOrigin.Digest(receipt.Before).SequenceEqual(value.TransferBefore)||
                        !RebirthProductiveEarnedMobileOrigin.Digest(receipt.After).SequenceEqual(value.TransferAfter))return false;
                    var prior=RebirthCamperTransferSnapshot.Restore(vehicle,receipt.Before).Stations.Find(installation);
                    var next=RebirthCamperTransferSnapshot.Restore(vehicle,receipt.After).Stations.Find(installation);
                    if(prior==null||next==null||!original.Artifact.MatchesMarker(prior.State.CopyProductiveEarnedMarker())||
                        !SameMobileGrid(new[]{prior.InstalledItem},new[]{installed})||!SameMobileGrid(new[]{next.InstalledItem},new[]{installed})||
                        !original.Artifact.MatchesMarker(next.State.CopyProductiveEarnedMarker())||
                        !RebirthProductiveEarnedMobileOrigin.Canonical(prior.State,installed).SequenceEqual(value.Before)||
                        !RebirthProductiveEarnedMobileOrigin.Canonical(next.State,installed).SequenceEqual(value.After))return false;
                    if(!ReadMobileRecord(value.Path+".owner",out var ownerOriginal)||
                        !RebirthCamperSavedCursor.VerifyPublishedOwnerImage(ownerOriginal,value.Operation))return false;
                    if(!value.Current())
                    {
                        if(value.After.SequenceEqual(loadedBody))
                        {if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(value.Path+".saved",RebirthProductiveEarnedMobileOrigin.Digest(value.Record)))return false;}
                        else if(value.Before.SequenceEqual(loadedBody)&&receipt.State==RebirthCamperTransferJournal.Phase.Uncertain)
                        {
                            if(File.Exists(value.Path+".saved")||File.Exists(value.Path+".saved.tmp")||
                                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*record.Length+65536),out value.Retention))return false;
                            value.Reentry=true;
                            value.BeforeHash=RebirthProductiveEarnedMobileOrigin.Digest(value.Before);value.AfterHash=RebirthProductiveEarnedMobileOrigin.Digest(value.After);
                        }
                        else return false;
                    }
                    if(!value.Reentry&&!value.Compact())return false;
                    candidates.Add(value);
                }
                }
            }
            byte[] current=RebirthProductiveEarnedMobileOrigin.Digest(before);
            while(candidates.Count!=0)
            {
                var matching=candidates.Where(value=>value.BeforeHash.SequenceEqual(current)).ToArray();
                if(matching.Length!=1)return false;
                var next=matching[0];candidates.Remove(next);
                if(next.Reentry)
                {
                    if(candidates.Count!=0){next.Dispose();return false;}
                    pending=next;after=next.Before;accepted=true;return true;
                }
                sources.Add(next);current=next.AfterHash;
            }
            if(sources.Count!=0)
            {
                var last=sources[sources.Count-1];
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4*last.RecordLength+65536),out var workspace))return false;
                using(workspace)
                {
                    if(!ReadMobileRecord(last.Path,out var bytes)||!RebirthProductiveEarnedMobileOrigin.Digest(bytes).SequenceEqual(last.RecordHash))return false;
                    using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
                    {
                        reader.ReadInt32();reader.ReadBytes(32+16+16+16);reader.ReadString();reader.ReadString();reader.ReadBytes(32+32+32);
                        ReadMobileBody(reader);byte[] body=ReadMobileBody(reader);
                        if(stream.Position!=stream.Length||!RebirthProductiveEarnedMobileOrigin.Digest(body).SequenceEqual(last.AfterHash)||
                            !RebirthProductiveEarnedObservationBudget.TryResizeDetached(last.Retention,checked(4096L+2L*(last.Path.Length+last.Account.Length+last.Creation.Length)+body.Length)))return false;
                        last.After=body;after=body;
                    }
                }
            }
            accepted=true;return true;
        }
        catch{return false;}
        finally{if(!accepted){foreach(var value in candidates)value.Dispose();foreach(var value in sources)value.Dispose();sources.Clear();pending?.Dispose();pending=null;}}
    }
    private static bool MobileRemoteOwnerCurrent(MobileRemoteTools source)
    {
        try
        {
            string path=source.Path+".owner";
            if(source.OwnerDigest?.Length!=32||source.Account==null||source.Account.Length>4096||source.Creation==null||source.Creation.Length>4096||
                !RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp")||!File.Exists(path)||new FileInfo(path).Length>16*1024*1024)return false;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var sha=System.Security.Cryptography.SHA256.Create())return sha.ComputeHash(stream).SequenceEqual(source.OwnerDigest);
        }
        catch{return false;}
    }
    private static bool MobileRemoteRecordCurrent(MobileRemoteTools source)
    {
        try
        {
            if(source.RecordHash?.Length!=32||source.RecordLength<1||source.RecordLength>RebirthProductiveEarnedMobileOrigin.MaximumBytes||
                !RebirthProductiveEarnedArtifact.SafePath(source.Path)||File.Exists(source.Path+".tmp")||!File.Exists(source.Path)||
                new FileInfo(source.Path).Length!=source.RecordLength)return false;
            using(var stream=new FileStream(source.Path,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var sha=System.Security.Cryptography.SHA256.Create())return sha.ComputeHash(stream).SequenceEqual(source.RecordHash);
        }
        catch{return false;}
    }
}
