using System;
using System.IO;
using System.Linq;
using System.Text;

public static partial class RemoteResourceTransactions
{
    private sealed class ActionClientCheckpoint
    {
        internal ActionRemoteClick Click;internal bool Initial,Refund,Outcome,Refused,NativeSaved,Saved,Invoked,Retired;internal int Workers;
        internal string Directory,Path;internal PlayerDataFile Data;internal byte[] Native;internal IDisposable Memory;
#if REBIRTH_DEBUG
        internal bool ScenarioPaused;
#endif
    }
    private sealed class ActionClientCheckpointSave
    {internal ActionClientCheckpoint Checkpoint;internal byte[] Native;internal bool Finished;}
    [ThreadStatic]private static ActionClientCheckpoint actionClientCheckpointCapture;
#if REBIRTH_DEBUG
    private static Action<Guid,string> actionClientCheckpointBoundary;
#endif
    private static void ObserveActionClientCheckpointBoundary(ActionClientCheckpoint checkpoint,string boundary)
    {
#if REBIRTH_DEBUG
        if(ThreadManager.IsMainThread()&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest")
            try{actionClientCheckpointBoundary?.Invoke(checkpoint.Click.Operation,boundary);}
            catch{checkpoint.ScenarioPaused=true;throw;}
#endif
    }
    private static bool ActionClientCheckpointPaused(ActionClientCheckpoint checkpoint)
    {
#if REBIRTH_DEBUG
        return checkpoint.ScenarioPaused;
#else
        return false;
#endif
    }
    private static string ActionClientDirectory(ActionRemoteClick click)
    {
        string directory=Path.GetFullPath(Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","ActionOwnerClientOriginals",click.Operation.ToString("N")));
        ActionPath(directory);return directory;
    }
    // Complete immutable original material, never a setter permit on its own.
    // Exact byte comparison, bounded before allocation, also retains interrupted
    // writes as unknown rather than replacing them with another original.
    private static bool SaveActionClientRecord(string directory,string name,string text,byte[] native)
    {
        var encoding=new UTF8Encoding(false,true);int textBytes=encoding.GetByteCount(text??string.Empty);
        long length=checked(12L+textBytes+(native?.LongLength??0));
        if(length>24L*1024*1024+12||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(3*length+65536),out var memory))return false;
        using(memory)
        {
            ActionPath(directory);Directory.CreateDirectory(directory);string path=Path.Combine(directory,name);ActionPath(path);
            using(var stream=new MemoryStream((int)length))
            {
                using(var writer=new BinaryWriter(stream,encoding,true))
                {writer.Write(0x31434145u);writer.Write(textBytes);writer.Write(encoding.GetBytes(text??string.Empty));writer.Write(native?.Length??0);if(native!=null)writer.Write(native);writer.Flush();}
                byte[] bytes=stream.ToArray();
                if(File.Exists(path))return ActionEnvelopeFileEquals(path,bytes);
                using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
                return ActionEnvelopeFileEquals(path,bytes);
            }
        }
    }
    private static bool SaveActionClientOriginal(ActionRemoteClick click)
        =>click.CourierCurrent()&&click.InitialSaved&&click.BeforeObserved&&click.NativeBefore!=null&&SaveActionClientRecord(ActionClientDirectory(click),"original.bin",click.Original,click.NativeBefore);
    private static bool PublishActionClientHead(ActionClientCheckpoint checkpoint,string stage)
    {
        var click=checkpoint.Click;string text=stage+"\n"+(checkpoint.Initial?click.Original:click.Plan.Original);
        string name=stage+".bin";
        if(!SaveActionClientRecord(checkpoint.Directory,name,text,checkpoint.Native))return false;
        string immutable=Path.Combine(checkpoint.Directory,name),head=Path.Combine(checkpoint.Directory,"current.bin");ActionPath(head);
        long length=new FileInfo(immutable).Length;
        if(length<12||length>24L*1024*1024+12||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(length+65536),out var memory))return false;
        using(memory)
        {
            byte[] bytes=ActionBytes(immutable,(int)length);
            if(File.Exists(head))
            {
                if(ActionEnvelopeFileEquals(head,bytes)){click.ClientHeadStage=stage;return true;}
                if(click.ClientHeadStage==null)return false;
                string prior=Path.Combine(checkpoint.Directory,click.ClientHeadStage+".bin");ActionPath(prior);
                // Compare streams, not length/hash or missing-receipt inference.
                using(var priorFile=new FileStream(prior,FileMode.Open,FileAccess.Read,FileShare.Read))
                using(var current=new FileStream(head,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(priorFile.Length!=current.Length||priorFile.Length>24L*1024*1024+12)return false;
                    byte[] left=new byte[32768],right=new byte[32768];long remaining=priorFile.Length;
                    while(remaining>0)
                    {
                        int wanted=(int)Math.Min(left.Length,remaining),leftRead=0,rightRead=0;
                        while(leftRead<wanted){int n=priorFile.Read(left,leftRead,wanted-leftRead);if(n<1)return false;leftRead+=n;}
                        while(rightRead<wanted){int n=current.Read(right,rightRead,wanted-rightRead);if(n<1)return false;rightRead+=n;}
                        for(int index=0;index<wanted;index++)if(left[index]!=right[index])return false;
                        remaining-=wanted;
                    }
                    if(priorFile.ReadByte()!=-1||current.ReadByte()!=-1)return false;
                }
            }
            else if(click.ClientHeadStage!=null||stage!="initial-saved")return false;
            string temporary=Path.Combine(checkpoint.Directory,"current-"+stage+".pending");ActionPath(temporary);
            if(File.Exists(temporary)){if(!ActionEnvelopeFileEquals(temporary,bytes))return false;}
            else{using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}}
            if(!ActionEnvelopeFileEquals(temporary,bytes)||!RebirthDurableFileCommit.TryPublish(temporary,head,out _)||!ActionEnvelopeFileEquals(head,bytes))return false;
            click.ClientHeadStage=stage;return true;
        }
    }
    private static bool SaveActionClientInitialBefore(ActionRemoteClick click)
    {
        if(click.InitialSaved)return true;
        if(!click.Current()||actionClientCheckpointCapture!=null||click.Actor.Buffs.GetCustomVar(ActionOwnerMarker(click.Operation))!=1f)return false;
        var checkpoint=click.InitialCheckpoint;
        if(checkpoint==null)
        {
            string directory=ActionClientDirectory(click);
            if(!SaveActionClientRecord(directory,"click.bin",click.Original,null))return false;
            checkpoint=new ActionClientCheckpoint{Click=click,Initial=true,Directory=directory,
                Path=Path.GetFullPath(Path.Combine(GameIO.GetPlayerDataDir(),click.Account+"."+PlayerDataFile.EXT))};
            if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(checkpoint.Path))return false;
            click.InitialCheckpoint=checkpoint;
        }
        if(checkpoint.Workers!=0||checkpoint.Retired||ActionClientCheckpointPaused(checkpoint))return false;
        if(!checkpoint.NativeSaved)
        {actionClientCheckpointCapture=checkpoint;try{GameManager.Instance.SaveLocalPlayerData();}finally{actionClientCheckpointCapture=null;}}
        if(!checkpoint.NativeSaved||!click.Current()||ActionClientCheckpointPaused(checkpoint)||!PublishActionClientHead(checkpoint,"initial-saved"))return false;
        checkpoint.Saved=true;
        click.InitialSaved=true;ReleaseActionClientCheckpoint(checkpoint);click.InitialCheckpoint=null;return true;
    }
    private static bool BeginActionClientCheckpoint(ActionRemoteClick click,bool refund)
    {
        var checkpoint=refund?click.RefundCheckpoint:click.DebitCheckpoint;
        if(checkpoint==null)
        {
            if(!SaveActionClientRecord(ActionClientDirectory(click),refund?"refund-grant.bin":"debit-grant.bin",click.Plan.Original,click.Plan.HostNativeBefore))return false;
            checkpoint=new ActionClientCheckpoint{Click=click,Refund=refund,Directory=ActionClientDirectory(click),
                Path=Path.GetFullPath(Path.Combine(GameIO.GetPlayerDataDir(),click.Account+"."+PlayerDataFile.EXT))};
            if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(checkpoint.Path))return false;
            if(refund)click.RefundCheckpoint=checkpoint;else click.DebitCheckpoint=checkpoint;
        }
        if(checkpoint.Retired||checkpoint.Invoked||ActionClientCheckpointPaused(checkpoint))return false;
        if(!checkpoint.NativeSaved)
        {
            if(checkpoint.Workers!=0||actionClientCheckpointCapture!=null)return false;
            click.Actor.Buffs.SetCustomVar(ActionOwnerMarker(click.Operation),refund?5f:4f,true);
            actionClientCheckpointCapture=checkpoint;
            try{GameManager.Instance.SaveLocalPlayerData();}
            finally{actionClientCheckpointCapture=null;}
        }
        if(!checkpoint.NativeSaved||!click.Current()||ActionClientCheckpointPaused(checkpoint)||click.Actor.Buffs.GetCustomVar(ActionOwnerMarker(click.Operation))!=(refund?5f:4f))return false;
        checkpoint.Saved=true;
        // One synchronous original may cross this boundary once. Even an
        // interrupted invocation-record write consumes warm permission; cold
        // recovery never turns an invoking file into a new first invocation.
        checkpoint.Invoked=true;
        if(!PublishActionClientHead(checkpoint,refund?"refund-invoking":"debit-invoking"))return false;
        ObserveActionClientCheckpointBoundary(checkpoint,"invocation-saved");return true;
    }
    private static bool SaveActionClientOutcome(ActionRemoteClick click,float phase)
    {
        bool refund=phase==3f,refused=phase==8f;
        if(phase!=2f&&phase!=3f&&phase!=8f)return false;
        var checkpoint=refund?click.RefundOutcome:click.DebitOutcome;
        if(checkpoint==null)
        {
            checkpoint=new ActionClientCheckpoint{Click=click,Outcome=true,Refund=refund,Refused=refused,Directory=ActionClientDirectory(click),
                Path=Path.GetFullPath(Path.Combine(GameIO.GetPlayerDataDir(),click.Account+"."+PlayerDataFile.EXT))};
            if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(checkpoint.Path))return false;
            if(refund)click.RefundOutcome=checkpoint;else click.DebitOutcome=checkpoint;
        }
        if(checkpoint.Refused!=refused||checkpoint.Retired||checkpoint.Workers!=0||ActionClientCheckpointPaused(checkpoint))return false;
        if(checkpoint.Saved)return true;
        if(!checkpoint.NativeSaved)
        {
            if(!click.OwnerMetadataCurrent()||actionClientCheckpointCapture!=null)return false;
            actionClientCheckpointCapture=checkpoint;
            try{GameManager.Instance.SaveLocalPlayerData();}finally{actionClientCheckpointCapture=null;}
        }
        if(!checkpoint.NativeSaved||ActionClientCheckpointPaused(checkpoint)||!PublishActionClientHead(checkpoint,refused?"debit-refused":refund?"refund-returned":"debit-returned"))return false;
        checkpoint.Saved=true;return true;
    }
    private static bool SaveActionClientTerminal(ActionRemoteClick click,byte phase)
    {
        var outcome=phase==5?click.DebitOutcome:click.Refunded?click.RefundOutcome:click.DebitOutcome;
        byte[] native;
        if(!click.Plan.NoOwnerContribution)
        {
            if(outcome?.Saved!=true)
            {
                float completed=click.Actor.Buffs.GetCustomVar(ActionOwnerMarker(click.Operation));
                if(!SaveActionClientOutcome(click,completed))return false;
                outcome=phase==5?click.DebitOutcome:click.Refunded?click.RefundOutcome:click.DebitOutcome;
            }
            if(outcome?.Saved!=true)return false;native=outcome.Native;
        }
        else native=click.Plan.HostNativeBefore;
        var terminal=new ActionClientCheckpoint{Click=click,Directory=ActionClientDirectory(click),Native=native};
        return PublishActionClientHead(terminal,phase==5?"used-terminal":"returned-terminal");
    }
    private static void CaptureActionClientCheckpoint(PlayerDataFile data,EntityPlayer actor,bool ran)
    {
        var checkpoint=actionClientCheckpointCapture;var click=checkpoint?.Click;
        if(!ran||click==null||checkpoint.Saved||checkpoint.Retired||checkpoint.Workers!=0||!ReferenceEquals(actor,click.Actor)||!(checkpoint.Outcome?click.OwnerMetadataCurrent():click.Current())||data==null||data.bDead||data.id!=click.NativeEntityId||data.deaths!=click.Deaths||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,ActionOwnerMarker(click.Operation),out float phase,out bool present)||!present||
            phase!=(checkpoint.Initial?1f:checkpoint.Outcome?checkpoint.Refused?8f:checkpoint.Refund?3f:2f:checkpoint.Refund?5f:4f))return;
        if(!TryReserveActionOwnerProjection(data,out var projection))return;
        using(projection)
        {
            bool after=!checkpoint.Refused&&(checkpoint.Refund!=checkpoint.Outcome);
            if(!SameActionItems(RebirthGearPlayerDataInventory.ReadSlots(data,true),after?click.Plan.BagAfter:click.BagBefore)||
                !SameActionItems(RebirthGearPlayerDataInventory.ReadSlots(data,false),after?click.Plan.BeltAfter:click.BeltBefore))return;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var transfer))return;
            using(transfer)
            {
                byte[] native=ActionOwnerImage(data);
                if(checkpoint.Initial)
                {
                    byte[] previous=click.NativeBefore;click.NativeBefore=native;
                    if(!NarrowActionRemoteClickMemory(click)){click.NativeBefore=previous;return;}
                    click.BeforeObserved=true;checkpoint.Native=native;checkpoint.Data=data;return;
                }
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(native.LongLength+4096),out var retained))return;
                checkpoint.Memory?.Dispose();checkpoint.Memory=retained;checkpoint.Native=native;checkpoint.Data=data;
            }
        }
    }
    private static object EnterActionClientCheckpointSave(PlayerDataFile data,string directory,string name,bool ran)
    {
        var checkpoint=actionClientCheckpointCapture;
        if(!ran||checkpoint?.Click==null||!(checkpoint.Outcome?checkpoint.Click.OwnerMetadataCurrent():checkpoint.Click.Current())||checkpoint.Saved||checkpoint.Retired||checkpoint.Workers!=0||!ReferenceEquals(checkpoint.Data,data)||checkpoint.Native==null||
            Path.GetFullPath(Path.Combine(directory,name+"."+PlayerDataFile.EXT))!=checkpoint.Path||!checkpoint.Native.SequenceEqual(ActionOwnerImage(data)))return null;
        var save=new ActionClientCheckpointSave{Checkpoint=checkpoint,Native=checkpoint.Native};checkpoint.Workers++;return save;
    }
    private static void ObserveActionClientCheckpointSave(bool ran,ActionClientCheckpointSave save)
    {
        lock(actionOwnerGate)
        {
            if(save.Finished)return;save.Finished=true;var checkpoint=save.Checkpoint;
            try
            {
                if(!ran||checkpoint.Retired||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(checkpoint.Path,save.Native))return;
                string path=Path.Combine(checkpoint.Directory,checkpoint.Initial?"initial-before.native":checkpoint.Outcome?checkpoint.Refused?"debit-refused.native":checkpoint.Refund?"refund-after.native":"debit-after.native":checkpoint.Refund?"refund-before.native":"debit-before.native");ActionPath(path);
                if(File.Exists(path)){if(!ActionEnvelopeFileEquals(path,save.Native))return;}
                else{using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(save.Native,0,save.Native.Length);file.Flush(true);}if(!ActionEnvelopeFileEquals(path,save.Native))return;}
                checkpoint.NativeSaved=true;
                ObserveActionClientCheckpointBoundary(checkpoint,checkpoint.Initial?"initial-saved":"attempt-saved");
            }
            finally{checkpoint.Data=null;checkpoint.Workers--;save.Native=null;save.Checkpoint=null;if(checkpoint.Retired)ReleaseActionClientCheckpoint(checkpoint);}
        }
    }
    private static void ReleaseActionClientCheckpoint(ActionClientCheckpoint checkpoint)
    {
        if(checkpoint==null)return;checkpoint.Retired=true;checkpoint.Click=null;
        if(checkpoint.Workers!=0)return;
        checkpoint.Data=null;checkpoint.Native=null;checkpoint.Memory?.Dispose();checkpoint.Memory=null;
    }
}
