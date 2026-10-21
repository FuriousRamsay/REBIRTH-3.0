using System;
using System.Collections.Generic;
using System.Linq;

public static partial class RebirthDogNavigationMarkerService
{
    internal static RebirthNpcOwnerNativeContinuationAdoption CaptureNativeOwnerAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||!scope.IsUnchangedOriginal()||markerPublicationUncertain||
                markerWorld!=null&&!ReferenceEquals(markerWorld,scope.World))return null;
            var original=Markers;var warnings=WarnedFailures;var priorOwner=markerOwner??scope.World.GetPrimaryPlayer();var priorManager=markerManager;
            var rows=original.ToArray();string image=MarkerImage().OuterXml;
            string account=null,creation=null;int deaths=0;
            if(priorOwner!=null)
            {
                if(!RebirthStablePlayerIdentity.TryResolveServerEntity(priorOwner,out var owner))return null;
                account=owner.CanonicalId;creation=RebirthCamperCharacterAccess.CurrentCreation(priorOwner);deaths=priorOwner.Died;
                if(string.IsNullOrWhiteSpace(creation))return null;
            }
            else if(rows.Length!=0)return null;
            var removed=new HashSet<NavObject>();
            bool Current(){lock(Sync)return ReferenceEquals(Markers,original)&&ReferenceEquals(WarnedFailures,warnings)&&MarkerImage().OuterXml==image;}
            bool OriginalMember(NavObject marker,bool present)
            {
                if(marker==null||priorManager?.NavObjectList==null||!ReferenceEquals(marker.OwnerEntity,priorOwner)||
                    marker.TrackType!=NavObject.TrackTypes.Position||marker.NavObjectClass?.NavObjectClassName!=NavClass)return false;
                int count=0;foreach(var value in priorManager.NavObjectList)if(ReferenceEquals(value,marker))count++;
                return present?count==1:count==0;
            }
            World adopted=null;
            bool Adopt(World incoming,Dictionary<string,RebirthDogLifecycleService.NativeOwnerReentry> native)
            {
                lock(Sync)
                {
                    if(adopted!=null)return ReferenceEquals(adopted,incoming);
                    if(ReferenceEquals(Markers,original)||markerPublicationUncertain)return false;
                    if(account==null){if(rows.Length!=0)return false;adopted=incoming;return true;}
                    if(!native.TryGetValue(account,out var source)||!source.Current()||source.Creation!=creation||source.Deaths!=deaths||
                        source.Actor is not EntityPlayerLocal player||!ReferenceEquals(incoming.GetPrimaryPlayer(),player))return false;
                    foreach(var row in rows)if(!OriginalMember(row.Value,!removed.Contains(row.Value)))return false;
                    var persistent=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
                    if(persistent?.PrimaryId==null)return false;
                    var desired=BuildRows(incoming,player,persistent.PrimaryId);
                    foreach(var row in rows)if(!removed.Contains(row.Value))
                    {
                        try{priorManager.UnRegisterNavObject(row.Value);}finally{if(OriginalMember(row.Value,false))removed.Add(row.Value);}
                        if(!removed.Contains(row.Value))return false;
                    }
                    if(!RequireMarkerScope())return false;
                    ReceiveNetworkSnapshot(desired,true);
                    foreach(var row in desired)if(row.Lifecycle==RebirthDogLifecycleKind.Active&&
                        (!Markers.TryGetValue(row.StableId,out var marker)||!OwnMarker(marker)))return false;
                    original.Clear();warnings.Clear();adopted=incoming;return true;
                }
            }
            var accounts=new HashSet<string>(StringComparer.Ordinal);if(account!=null)accounts.Add(account);
            return Current()?new RebirthNpcOwnerNativeContinuationAdoption(scope,accounts,Current,Adopt):null;
        }
    }
}
