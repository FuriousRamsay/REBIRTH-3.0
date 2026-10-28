// Deferred source-wrapper checks. Native serializers, disk and observers are
// explicit doubles here; this does not qualify saved PDS or Harmony ordering.
using System;
using System.Collections.Generic;

sealed class PlayerDataFile {}
static class Probe
{
    internal static readonly List<string> Events=new List<string>();
    internal static bool ThrowHostBefore,ThrowHostAfter,ThrowLegacy,ThrowCancellation;
    internal static void Reset(){Events.Clear();ThrowHostBefore=ThrowHostAfter=ThrowLegacy=ThrowCancellation=false;}
    internal static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    internal static int Count(string value)=>Events.FindAll(item=>item==value).Count;
}
static class RebirthRemoteResourcePlacedStationOriginal
{
    internal static void EnterCancellationOwnerSave(PlayerDataFile data,string dir,string id,bool run,out object state)
    {state=new object();Probe.Events.Add("c-enter");}
    internal static void ObserveCancellationOwnerSave(bool ran,object state)
    {Probe.Assert(state!=null,"Lost cancellation scope");Probe.Events.Add("c-finish:"+ran);if(Probe.ThrowCancellation)throw new Exception("observer");}
}
static class RebirthProductiveEarnedUiDeferredTransport
{
    internal static class OwnerPds
    {
        internal static void BeforeSave(PlayerDataFile data,string dir,string id,bool run,out object state)
        {state=new object();Probe.Events.Add("h-enter");if(Probe.ThrowHostBefore)throw new Exception("prefix");}
        internal static void AfterSave(bool ran,object state)
        {Probe.Events.Add("h-finish:"+ran);if(Probe.ThrowHostAfter)throw new Exception("observer");}
    }
}
static class Fixture
{
    private sealed class NativeFile {}
    private static void ReleaseDeferredSaveState(NativeFile state){Probe.Events.Add("release");}
    private static class Save
    {
        // PRODUCTION_SAVE_WRAPPER
        private static void PrefixLegacy(PlayerDataFile data,string dir,string id,bool run,out NativeFile state)
        {state=new NativeFile();Probe.Events.Add("legacy-enter");}
        private static void PostfixLegacy(bool ran,NativeFile state)
        {Probe.Events.Add("legacy-finish");if(Probe.ThrowLegacy)throw new Exception("legacy");}
        internal static void Exercise(bool ran,bool finalizerOnly=false)
        {
            Observation state=null;
            try{Prefix(new PlayerDataFile(),"dir","id",true,out state);}
            catch(Exception failure)
            {Probe.Assert(ReferenceEquals(Finalizer(failure,state),failure),"Prefix exception replaced");return;}
            var nativeFailure=finalizerOnly?new Exception("native"):null;
            if(!finalizerOnly)try{Postfix(ran,state);}catch{}
            Probe.Assert(ReferenceEquals(Finalizer(nativeFailure,state),nativeFailure),"Native exception replaced");
            Probe.Assert(state.Finished&&state.Native==null&&state.HostPds==null&&state.CancellationOwner==null,"State graph retained");
        }
    }
    static void Main()
    {
        Probe.Reset();Save.Exercise(true);
        Probe.Assert(Probe.Count("c-finish:True")==1&&Probe.Count("h-finish:True")==1&&Probe.Count("release")==1,"Normal callbacks not once");
        Probe.Reset();Probe.ThrowLegacy=true;Save.Exercise(true);
        Probe.Assert(Probe.Count("c-finish:True")==1&&Probe.Count("release")==1,"Legacy throw skipped independent observer");
        Probe.Reset();Probe.ThrowHostAfter=true;Probe.ThrowCancellation=true;Save.Exercise(true);
        Probe.Assert(Probe.Count("c-finish:True")==1&&Probe.Count("release")==1,"Observer throw leaked cleanup");
        Probe.Reset();Save.Exercise(false);
        Probe.Assert(Probe.Count("c-finish:False")==1&&Probe.Count("c-finish:True")==0,"Skipped native became saved");
        Probe.Reset();Save.Exercise(false,true);
        Probe.Assert(Probe.Count("c-finish:False")==1&&Probe.Count("release")==1,"Native exception failed cleanup");
        Probe.Reset();Probe.ThrowHostBefore=true;Save.Exercise(false);
        Probe.Assert(Probe.Count("c-finish:False")==1&&Probe.Count("release")==1,"Prefix throw lost earlier cancellation scope");
    }
}
