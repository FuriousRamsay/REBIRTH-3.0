using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    // A viewer asks to observe an already paid native pending source. The
    // request cannot create earned work or change its original recipient.
    internal static bool BeginViewerTools(EntityPlayer actor,ClientInfo peer,TileEntityWorkstation station,string creation)
    {
        RebirthProductiveEarnedCoordinator.SavedPendingToolsObservation source=null;
        RebirthProductiveEarnedObservationBudget memory=null;
        IDisposable viewerMemory=null;
        try
        {
            if(!RebirthProductiveEarnedCoordinator.Enabled||!installed||!RebirthProductiveEarnedArtifact.RemoteAuthority(actor,peer,station,creation)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var payer))return false;
            ObserveWorld();
            var existing=pending.Values.FirstOrDefault(value=>ReferenceEquals(value.Station,station));
            if(existing!=null)
            {
                if(existing.ViewerToolsOnly&&!existing.ViewerToolsReleased&&existing.Tools?.Saved==true&&ReferenceEquals(existing.Actor,actor)&&ReferenceEquals(existing.Sender,peer))FinishViewerToolsHandoff(existing);
                if(!existing.ViewerToolsOnly||!ReferenceEquals(existing.Actor,actor)||!ReferenceEquals(existing.Sender,peer)||!existing.Current())return false;
                SendOriginalNativeEcho(actor,peer,existing.Operation,existing.Nonce,Digest(existing.Image));return true;
            }
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||pending.Count>=4||!RebirthProductiveEarnedCoordinator.TryObserveSavedPendingTools(station,decoder,out source))return false;
                byte[] image=source.CopyOriginalImage();var frame=RebirthProductiveEarnedRowProof.Decode(image);
                if(!decoder.Admit(frame,image.Length))return false;
                memory=RebirthProductiveEarnedObservationBudget.TryOpen(source.OriginalArtifact,frame);if(memory==null)return false;
                string save=Path.GetFullPath(GameIO.GetSaveGameDir()),path=Path.Combine(save,"RebirthProductiveEarned","remote",payer.StorageKey,"native-viewer",frame.Operation.ToString("N")+".header");
                byte[] record=File.Exists(path+".nativeViewerSource")?ReadNativeOutputMetadata(path+".nativeViewerSource",RebirthProductiveEarnedRowProof.MaximumArtifactBytes):RebirthProductiveEarnedViewerToolSource.Capture(source);
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*record.Length+65536),out viewerMemory))return false;
                Guid nonce=Guid.NewGuid();byte[] header;
                if(File.Exists(path))
                {
                    header=Read(path);
                    if(!TryReadHeader(header,payer.StorageKey,creation,actor.Died,station,frame,Digest(image),out nonce))return false;
                }
                else header=Header(payer.StorageKey,creation,actor.Died,station,frame,Digest(image),nonce);
                if(!RebirthProductiveEarnedViewerToolSource.TryRead(source.OriginalPath,image,record,nonce,out var paid)||
                    !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(path,header)||
                    !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(path+".nativeViewerSource",record,RebirthProductiveEarnedRowProof.MaximumArtifactBytes))return false;
                var context=new Context{World=actor.world,Actor=actor,Sender=peer,Station=station,Artifact=source.OriginalArtifact,Image=image,Header=header,HeaderPath=path,
                    Directory=save,WorldKey=frame.World,Operation=frame.Operation,Nonce=nonce,Owner=payer.StorageKey,Creation=creation,Deaths=actor.Died,Memory=memory,
                    ViewerToolsOnly=true,ViewerToolsSource=source,ViewerToolsRecord=record,ViewerToolsRetention=viewerMemory,NativeOutput=paid,NativeOutputAccepted=true,NativeOutputSaved=true};
                if(!context.Current())return false;
                pending.Add(context.Operation,context);memory=null;source=null;viewerMemory=null;
                if(File.Exists(path+".nativeTools")&&!RestoreNativeToolsDescription(context))return false;
                if(context.Tools?.Saved==true&&!FinishViewerToolsHandoff(context))return false;
                SendOriginalNativeEcho(actor,peer,context.Operation,context.Nonce,Digest(context.Image));return true;
            }
        }
        catch{return false;}
        finally{memory?.Dispose();source?.Dispose();viewerMemory?.Dispose();}
    }
    private static bool ViewerToolSourceCurrent(Context source)
        =>source?.ViewerToolsOnly==true&&source.Current()&&(source.ViewerToolsReleased?source.Tools?.Saved==true:source.ViewerToolsSource?.RetainedContext()==true)&&
            RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativeViewerSource",source.ViewerToolsRecord,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
    private static bool FinishViewerToolsHandoff(Context source)
    {
        if(source.ViewerToolsReleased)return true;
        var tools=source.Tools;
        if(tools?.Saved!=true||!RebirthProductiveEarnedSavedNativeSource.TryRemoteHandoffToolDependencies(source.Artifact.OriginalPath,source.Image,tools.Plan.After,tools.Plan.AfterOffset,out var dependencies))return false;
        var artifact=source.Artifact;
        Func<bool> saved=()=>artifact.Current()&&RebirthProductiveEarnedSavedNativeSource.RemoteHandoffDependenciesCurrent(dependencies);
        if(!RebirthProductiveEarnedCoordinator.HasAdoptedRemoteViewerTools(source.Station,source.Operation,tools.Plan.After,tools.Plan.AfterOffset)&&
            !source.ViewerToolsSource.AdoptSavedRemoteViewerTools(tools.Plan.After,tools.Plan.AfterOffset,saved))return false;
        byte[] hold=ReadNativeOutputMetadata(artifact.OriginalPath+".viewer-tools-hold",4096);
        if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(artifact.OriginalPath+".viewer-tools-released",System.Text.Encoding.ASCII.GetBytes(Digest(hold))))return false;
        source.ViewerToolsReleased=true;source.ViewerToolsSource.Dispose();source.ViewerToolsSource=null;return true;
    }
    internal static bool NativeViewerToolsHeld(string original)
    {
        try
        {
            string path=original+".viewer-tools-hold";
            if(File.Exists(path+".tmp"))return true;
            if(!File.Exists(path))return false;
            byte[] hold=ReadNativeOutputMetadata(path,4096);
            return !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(original+".viewer-tools-released",System.Text.Encoding.ASCII.GetBytes(Digest(hold)));
        }
        catch{return true;}
    }
    private static bool RetainViewerToolPaymentHold(Context source)
    {
        byte[] hold=System.Text.Encoding.UTF8.GetBytes(source.HeaderPath+"\n"+Digest(source.Header)+"\n"+Digest(source.ViewerToolsRecord));
        return RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(source.Artifact.OriginalPath+".viewer-tools-hold",hold);
    }
    internal static bool NativeViewerToolsRuntimeHeld(TileEntityWorkstation station)
        =>pending.Values.Any(value=>value.ViewerToolsOnly&&!value.ViewerToolsReleased&&ReferenceEquals(value.Station,station)&&
            RebirthProductiveEarnedArtifact.RemoteAuthority(value.Actor,value.Sender,station,value.Creation));
    private static void PruneReleasedViewerTools()
    {
        foreach(var value in pending.Values.Where(item=>item.ViewerToolsOnly&&(item.ViewerToolsReleased&&item.Tools?.Saved==true||item.Tools==null)).ToArray())
        {
            if(ReferenceEquals(value.World,GameManager.Instance?.World)&&RebirthProductiveEarnedArtifact.RemoteAuthority(value.Actor,value.Sender,value.Station,value.Creation)&&
                (value.Tools==null||RebirthProductiveEarnedNativeBody.MatchesCurrent(value.Station,value.Tools.Plan.After,value.Tools.Plan.AfterOffset)))continue;
            // Positive saved handoff already installed the native producer.
            // Retire only this payer's now-obsolete transport graph.
            value.Tools?.Dispose();value.Tools=null;value.ViewerToolsSource?.Dispose();value.ViewerToolsSource=null;
            value.Memory?.Dispose();value.Memory=null;pending.Remove(value.Operation);
            value.ViewerToolsRetention?.Dispose();value.ViewerToolsRetention=null;
        }
    }
}
