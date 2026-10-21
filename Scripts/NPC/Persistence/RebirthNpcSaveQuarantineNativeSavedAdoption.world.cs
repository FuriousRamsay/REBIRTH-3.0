using System;
using System.IO;
using System.Xml;

public static partial class RebirthNpcWorldIntegrationService
{
    internal static RebirthNpcSaveQuarantineNativeSavedStore CaptureNativeSavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!loaded&&scope!=null)
            {
                var originalEpoch=worldOriginalEpoch;var originalStable=ByStable;var originalPending=PendingSpawns;
                bool PrivateEmpty()=>!loaded&&!dirty&&!worldSnapshotPublicationUncertain&&!SpawnAttempts.HasClaims&&loadedNativeWorld==null&&
                    string.IsNullOrEmpty(loadedNativeDirectory)&&worldOriginalBinding==null&&ByStable.Count==0&&ByAmbient.Count==0&&Markers.Count==0&&PendingSpawns.Count==0&&Replay.Count==0&&Completions.Count==0;
                bool OriginalEmpty(){lock(Sync)return PrivateEmpty()&&ReferenceEquals(worldOriginalEpoch,originalEpoch)&&ReferenceEquals(ByStable,originalStable)&&ReferenceEquals(PendingSpawns,originalPending);}
                bool DetachedEmpty(){lock(Sync)return PrivateEmpty()&&!ReferenceEquals(worldOriginalEpoch,originalEpoch)&&!ReferenceEquals(ByStable,originalStable)&&!ReferenceEquals(PendingSpawns,originalPending);}
                bool IncomingOwner(World incoming){lock(Sync)return loaded&&!dirty&&!worldSnapshotPublicationUncertain&&!SpawnAttempts.HasClaims&&
                    ReferenceEquals(loadedNativeWorld,incoming)&&RebirthNpcSaveQuarantineScope.SameDirectory(loadedNativeDirectory,scope.Directory)&&Markers.Count==0;}
                void LoadOwner(){lock(Sync){if(DetachedEmpty())LoadNoLock();}}
                return RebirthNpcSaveQuarantineNativeSavedStore.CaptureUninitializedOwner("world-integration",scope,Path.Combine(scope.Directory,FileName),
                    OriginalEmpty,DetachedEmpty,IncomingOwner,document=>RebirthNpcWorldIdentityLoad.TryRead(document?.DocumentElement,out _,out _,out _,out _),CaptureRecoveryDocumentNoLock,LoadOwner);
            }
            if(!loaded||dirty||worldSnapshotPublicationUncertain||SpawnAttempts.HasClaims||loadedNativeWorld==null||string.IsNullOrEmpty(loadedNativeDirectory))return null;
            foreach(var record in ByStable.Values)if(record==null||!record.Persistent)return null;
            var world=loadedNativeWorld;string directory=loadedNativeDirectory,path=Path.Combine(directory,FileName);
            var epoch=worldOriginalEpoch;var binding=worldOriginalBinding;
            var stable=ByStable;var ambient=ByAmbient;var markers=Markers;var pending=PendingSpawns;var replay=Replay;var completions=Completions;
            bool metadataOnly=Markers.Count==0&&!SpawnAttempts.HasClaims;
            var primary=CaptureRecoveryDocumentNoLock();string image=primary.DocumentElement.OuterXml,originalImage=QuarantineWorldIntegrationImage().OuterXml;
            if(!RebirthNpcWorldIdentityLoad.TryRead(primary.DocumentElement,out var identities,out _,out _,out _))return null;
            bool Original(){lock(Sync)return loaded&&!dirty&&!worldSnapshotPublicationUncertain&&!SpawnAttempts.HasClaims&&
                ReferenceEquals(loadedNativeWorld,world)&&loadedNativeDirectory==directory&&ReferenceEquals(worldOriginalEpoch,epoch)&&
                ReferenceEquals(worldOriginalBinding,binding)&&ReferenceEquals(ByStable,stable)&&ReferenceEquals(ByAmbient,ambient)&&
                ReferenceEquals(Markers,markers)&&ReferenceEquals(PendingSpawns,pending)&&ReferenceEquals(Replay,replay)&&ReferenceEquals(Completions,completions)&&
                QuarantineWorldIntegrationImage().OuterXml==originalImage;}
            bool Empty(){lock(Sync)return !loaded&&!dirty&&!worldSnapshotPublicationUncertain&&!SpawnAttempts.HasClaims&&
                loadedNativeWorld==null&&string.IsNullOrEmpty(loadedNativeDirectory)&&ByStable.Count==0&&ByAmbient.Count==0&&Markers.Count==0&&
                PendingSpawns.Count==0&&Replay.Count==0&&Completions.Count==0&&!ReferenceEquals(worldOriginalEpoch,epoch)&&worldOriginalBinding==null;}
            bool Incoming(World incoming){lock(Sync)return loaded&&!dirty&&!worldSnapshotPublicationUncertain&&!SpawnAttempts.HasClaims&&
                ReferenceEquals(loadedNativeWorld,incoming)&&RebirthNpcSaveQuarantineScope.SameDirectory(directory,loadedNativeDirectory)&&Markers.Count==0&&
                CaptureRecoveryDocumentNoLock().DocumentElement.OuterXml==image;}
            bool Native(RebirthNpcRuntimeNativeAdoption.NativeReentry source)=>source?.Current()==true&&
                (!identities.TryGetValue(source.StableId,out var identity)||identity.ProfileId==source.Actor.RebirthProfileId);
            void Load(){lock(Sync){if(!Empty())return;LoadNoLock();}}
            return RebirthNpcSaveQuarantineNativeSavedStore.Capture("world-integration",scope,world,path,true,Original,Empty,Incoming,Native,
                document=>RebirthNpcWorldIdentityLoad.TryRead(document?.DocumentElement,out _,out _,out _,out _),primary,Load,
                source=>metadataOnly&&source?.Current()==true);
        }
    }
}
