using System;
using System.Linq;

public static partial class RemoteResourceTransactions
{
    // Preparation may reserve a plan, never invoke a financial setter. Only the
    // actual authenticated accepted AND saved native Before may enter this
    // synchronous owning scope. Ordinary PDS uploads are always left running.
    private static void PumpRemoteActionAdmissions()
    {
        if(!ThreadManager.IsMainThread()||actionRemoteAdmission!=null)return;
        ActionRemoteHostOriginal[] pending;
        lock(actionOwnerGate)pending=actionRemoteHostOriginals.Values.ToArray();
        foreach(var source in pending)
        {
#if REBIRTH_DEBUG
            if(source.AdmissionScenarioPaused)continue;
#endif
            if(source.NoAdmissionIssued){try{RecordRemoteActionNoAdmission(source,source.NoAdmissionReason);PumpRemoteActionNoAdmission(source);}catch(Exception error){RemoteResourceDiagnostics.Write("Electrical never-admitted original retained: "+error.Message);}continue;}
            if(source.AdmissionIssued||source.AdmissionEntered||source.Retired||!source.Accepted||!source.Saved||!source.Current()||
                System.Diagnostics.Stopwatch.GetTimestamp()<source.AdmissionNext)continue;
            source.AdmissionNext=System.Diagnostics.Stopwatch.GetTimestamp()+System.Diagnostics.Stopwatch.Frequency;
            try
            {
                if(!ConfirmRemoteActionBefore(source)||!RemoteActionOriginalCurrent(source))continue;
                // Existing payment membership is an actual retained graph, not
                // a lookup of current row equality or an imported receipt.
                lock(actionOwnerGate)
                {
                    if(actionPayments.Any(value=>ReferenceEquals(value.RemoteOwner,source)))
                    {source.AdmissionIssued=true;continue;}
                }
                if(!RecordActionRemoteAdmissionBoundary(source,"ready"))continue;
                if(!RecordActionRemoteAdmissionBoundary(source,"invoking"))continue;
                // The durable invocation checkpoint consumes admission even
                // when the owning call throws before returning a graph. An
                // interrupted invocation never regains first-entry authority.
                actionRemoteAdmission=source;
                try
                {
                    if(!RebirthInfrastructureWorkService.BeginOwnedService(source.Actor,0,source.Position,out var original,out var reason))
                    {
                        if(original!=null||actionPayments.Any(value=>ReferenceEquals(value.RemoteOwner,source)))
                            throw new InvalidOperationException("Refused preparation retained an owning original");
                        RecordRemoteActionNoAdmission(source,reason);continue;
                    }
                    if(original==null||!ReferenceEquals(original.RemoteOwner,source)||original.Operation!=source.Operation)
                        throw new InvalidOperationException("Electrical preparation returned a different owning original");
                    source.AdmissionIssued=true;
                }
                finally{actionRemoteAdmission=null;}
            }
            catch(Exception error)
            {
                // A preparation/refusal is not a financial no-effect receipt.
                // An issued invocation remains consumed. The retained payment,
                // if any, alone owns its native financial attempts.
                RemoteResourceDiagnostics.Write("Electrical native original admission retained: "+error.Message);
            }
        }
    }
}
