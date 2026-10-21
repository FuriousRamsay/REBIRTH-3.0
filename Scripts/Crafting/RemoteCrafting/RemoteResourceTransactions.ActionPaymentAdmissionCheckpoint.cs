using System;
using System.IO;
using System.Text;

public static partial class RemoteResourceTransactions
{
    // This WAL records entry to preparation, not an owner debit. A restart may
    // use a positive ready head only with independently reconstructed native
    // owner/target sources. Invoking is always uncertain without its actual
    // owning outcome; absence of a payment directory proves nothing.
    private static bool RecordActionRemoteAdmissionBoundary(ActionRemoteHostOriginal source,string stage)
    {
        if(!ThreadManager.IsMainThread()||!RemoteActionOriginalCurrent(source)||source.AdmissionIssued||source.AdmissionEntered||
            stage!="ready"&&stage!="invoking"||stage=="invoking"&&source.AdmissionStage!="ready")return false;
        long encoded=checked(12L+Encoding.UTF8.GetByteCount(source.Click)+stage.Length+1+source.NativeBefore.LongLength);
        if(encoded>24L*1024*1024+12||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4*encoded+2L*(source.Click.Length+stage.Length+1)+65536),out var memory))return false;
        using(memory)
        {
            string record=Path.Combine(source.Directory,"admission-"+stage+".native");
            string head=Path.Combine(source.Directory,"admission-current.native");
            string pending=Path.Combine(source.Directory,"admission-"+stage+".pending");
            ActionPath(record);ActionPath(head);ActionPath(pending);
            byte[] bytes=EncodeActionRemoteEnvelope(stage+"\n"+source.Click,source.NativeBefore);
            if(File.Exists(record))
            {if(!ActionEnvelopeFileEquals(record,bytes))return false;}
            else
                using(var output=new FileStream(record,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {output.Write(bytes,0,bytes.Length);output.Flush(true);}
            if(!ActionEnvelopeFileEquals(record,bytes))return false;
            if(File.Exists(head))
            {
                if(ActionEnvelopeFileEquals(head,bytes))
                {
                    source.AdmissionStage=stage;
                    // An already-published invoking head is historical entry,
                    // not a newly issued synchronous call permission.
                    if(stage=="invoking"){source.AdmissionEntered=true;return false;}
                    return true;
                }
                if(stage!="invoking")return false;
                byte[] prior=EncodeActionRemoteEnvelope("ready\n"+source.Click,source.NativeBefore);
                if(!ActionEnvelopeFileEquals(head,prior))return false;
            }
            else if(stage!="ready")return false;
            if(File.Exists(pending))
            {if(!ActionEnvelopeFileEquals(pending,bytes))return false;}
            else
                using(var output=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {output.Write(bytes,0,bytes.Length);output.Flush(true);}
            if(!ActionEnvelopeFileEquals(pending,bytes)||!RebirthDurableFileCommit.TryPublish(pending,head,out _)||!ActionEnvelopeFileEquals(head,bytes))return false;
            source.AdmissionStage=stage;
            if(stage=="invoking")source.AdmissionEntered=true;
            ObserveActionAdmissionBoundary(source,stage);
            return true;
        }
    }
#if REBIRTH_DEBUG
    private static Action<Guid,string> actionAdmissionBoundary;
#endif
    private static void ObserveActionAdmissionBoundary(ActionRemoteHostOriginal source,string stage)
    {
#if REBIRTH_DEBUG
        if(ThreadManager.IsMainThread()&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest")
            actionAdmissionBoundary?.Invoke(source.Operation,stage);
#endif
    }
}
