using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;

internal static partial class RebirthProductiveEarnedPendingToolGestures
{
    // Purpose-bound native owner observation. This cannot replace an accepted
    // station publication, native tool action or rebase receipt.
    internal sealed class OwnerCapture:IDisposable
    {
        internal readonly Guid Gesture;internal readonly int Steps;
        internal readonly bool Before;
        internal readonly string Root,Name,SaveDirectory;
        private World World;private Frame Frame;
        private byte[] image;private IDisposable memory;private int readers;
        private bool closed,saved,journalPublicationAttempted;private string finalPath;
        internal OwnerCapture(Observation scope,byte[] bytes,IDisposable allocation,string root,string name,string save,bool before)
        {Before=before;Gesture=scope.Operation;Steps=scope.Steps;World=scope.World;Frame=scope.After;image=bytes;memory=allocation;Root=root;Name=name;SaveDirectory=save;}
        internal bool Current(Observation scope)
        {
            lock(ownerGate)return !closed&&scope!=null&&(Before?ReferenceEquals(scope.OwnerBefore,this)&&scope.BeforeContext():ReferenceEquals(scope.OwnerAfter,this)&&scope.ReturnedContext())&&
                scope.Operation==Gesture&&scope.Steps==Steps&&ReferenceEquals(scope.After,Frame)&&ReferenceEquals(scope.World,World)&&
                Root==Path.GetFullPath(GameIO.GetPlayerDataDir())&&SaveDirectory==Path.GetFullPath(GameIO.GetSaveGameDir())&&
                RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.CanonicalId==Name;
        }
        internal bool Saved(Observation scope)
        {lock(ownerGate)return Current(scope)&&saved&&readers==0&&image!=null&&RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(finalPath,image);}
        internal bool WriteSavedBefore(Observation scope,Func<byte[],string,bool> write)
        {lock(ownerGate)return Before&&Saved(scope)&&write(image,finalPath);}
        internal bool WriteSavedAfter(Observation scope,Func<byte[],string,bool> write)
        {lock(ownerGate)return !Before&&Saved(scope)&&write(image,finalPath);}
        internal bool TryBeginJournalPublication()
        {
            lock(ownerGate)
            {
                if(Before||closed||!saved||readers!=0||journalPublicationAttempted)return false;
                journalPublicationAttempted=true;return true;
            }
        }
        internal SaveCall Enter(PlayerDataFile data,string directory,string name,bool run)
        {
            lock(ownerGate)
            {
                if(!run||closed||memory==null||name!=Name||Path.GetFullPath(directory)!=Root)return null;
                // A newer native save for this captured object supersedes the
                // old positive observation even if its new bytes do not match.
                saved=false;finalPath=null;
                string path=Path.GetFullPath(Path.Combine(Root,Name+"."+PlayerDataFile.EXT));
                if(Path.GetDirectoryName(path)!=Root||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path))return null;
                // Only immutable capture fields are used on a native Save worker.
                var actual=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);
                if(!actual.SequenceEqual(image))return null;
                readers++;return new SaveCall{Capture=this,Path=path};
            }
        }
        internal void Finish(SaveCall call,bool ran)
        {
            lock(ownerGate)
            {
                if(call.Finished)return;call.Finished=true;
                readers--;
                try
                {
                    if(ran&&!closed&&readers==0&&image!=null&&RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(call.Path,image))
                    {saved=true;finalPath=call.Path;journalPublicationAttempted=false;}
                }
                finally{if(closed&&readers==0)Release();}
            }
        }
        private void Release(){image=null;World=null;Frame=null;var held=memory;memory=null;held?.Dispose();}
        public void Dispose(){lock(ownerGate){if(closed)return;closed=true;saved=false;finalPath=null;if(readers==0)Release();}}
    }
    internal sealed class SaveCall {internal OwnerCapture Capture;internal string Path;internal bool Finished;}
    private static readonly object ownerGate=new object();
    private static readonly ConditionalWeakTable<PlayerDataFile,OwnerCapture> ownerCaptures=new ConditionalWeakTable<PlayerDataFile,OwnerCapture>();
    private static void InstallOwnerObserver()
    {
        var harmony=new Harmony("rebirth.productive.pending.tools.owner.observer");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(OwnerFromPlayer));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(OwnerSave));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(StationSyncObserver));
    }
    internal static bool TryObserveSavedGestureOwner(EntityPlayerLocal actor,TileEntityWorkstation station,out OwnerCapture capture)
    {
        capture=null;
        try
        {
            if(!TryObserveReturnedGesture(actor,station,out var observed)||observed.OwnerAfter?.Saved(observed)!=true)return false;
            capture=observed.OwnerAfter;return true;
        }
        catch{return false;}
    }
    private static void ObserveOwnerCapture(PlayerDataFile data,EntityPlayer player,bool ran)
    {
        if(!ran||!ThreadManager.IsMainThread()||!(player is EntityPlayerLocal actor)||data==null)return;
        IDisposable allocation=null;OwnerCapture created=null;
        try
        {
            // FromPlayer can reuse a mutable PlayerDataFile. A failed new
            // capture must retire that object's earlier successful event.
            lock(ownerGate)
            {
                if(ownerCaptures.TryGetValue(data,out var previous))previous.Dispose();
                ownerCaptures.Remove(data);
            }
            // More than one station cohort for one owner cannot select a save.
            var candidates=observations.Values.Where(value=>ReferenceEquals(value.Actor,actor)&&!value.Closed).ToArray();
            if(candidates.Length!=1)return;var scope=candidates[0];
            bool before=scope.Steps==0;
            if((before?!scope.BeforeContext():!TryObserveReturnedGesture(actor,scope.Station,out var returned)||!ReferenceEquals(returned,scope))||data.id!=actor.entityId||data.deaths!=scope.Deaths)return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out allocation))return;
                // The native owner file has no cursor slot. A settled cursor is
                // mandatory; a file must not omit a still-held tool's custody.
                if(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(new[]{ItemStack.Empty},262144,349528,out var emptyCursor)||scope.After.Cells[3]!=emptyCursor||
                    !RebirthProductiveEarnedArtifact.SameTools(RebirthGearPlayerDataInventory.ReadSlots(data,true),Convert.FromBase64String(scope.After.Cells[0]))||
                    !RebirthProductiveEarnedArtifact.SameTools(RebirthGearPlayerDataInventory.ReadSlots(data,false),Convert.FromBase64String(scope.After.Cells[1]))||
                    !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return;
                var image=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);
                var live=Frame.Capture(actor,scope.Tools);
                if(image.Length<1||image.Length>16*1024*1024||live==null||!scope.After.Same(live)||(before?!scope.BeforeContext():!scope.ReturnedContext()))return;
                created=new OwnerCapture(scope,image,allocation,Path.GetFullPath(GameIO.GetPlayerDataDir()),owner.CanonicalId,Path.GetFullPath(GameIO.GetSaveGameDir()),before);allocation=null;
                lock(ownerGate)
                {
                    ownerCaptures.Remove(data);
                    if(before){scope.OwnerBefore?.Dispose();scope.OwnerBefore=created;}else{scope.OwnerAfter?.Dispose();scope.OwnerAfter=created;}
                    ownerCaptures.Add(data,created);created=null;
                }
            }
        }
        catch{/* Unknown native capture grants no saved observation. */}
        finally{created?.Dispose();allocation?.Dispose();}
    }
    [HarmonyPatch(typeof(PlayerDataFile),nameof(PlayerDataFile.FromPlayer))]
    private static class OwnerFromPlayer
    {private static void Postfix(PlayerDataFile __instance,EntityPlayer _player,bool __runOriginal)=>ObserveOwnerCapture(__instance,_player,__runOriginal);}
    [HarmonyPatch(typeof(PlayerDataFile),nameof(PlayerDataFile.Save))]
    private static class OwnerSave
    {
        private static void Prefix(PlayerDataFile __instance,string _dir,string _playerId,bool __runOriginal,out SaveCall __state)
        {
            __state=null;
            try{lock(ownerGate)if(ownerCaptures.TryGetValue(__instance,out var capture))__state=capture.Enter(__instance,_dir,_playerId,__runOriginal);}catch{}
        }
        private static void Postfix(bool __runOriginal,SaveCall __state)
        {try{__state?.Capture.Finish(__state,__runOriginal);}catch{}}
        private static Exception Finalizer(Exception __exception,SaveCall __state)
        {try{__state?.Capture.Finish(__state,false);}catch{}return __exception;}
    }
}
