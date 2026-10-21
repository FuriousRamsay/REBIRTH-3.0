using System;

internal static partial class RebirthProductiveEarnedPendingToolGestures
{
    internal static bool CanEnterMobileTools(XUiC_WorkstationWindowGroup window)
    {
        try
        {
            var station=window?.WorkstationData?.TileEntity as RebirthCamperStationState;
            if(station==null||!observations.TryGetValue(station,out var scope)||!ReferenceEquals(scope.Window,window)||
                !scope.Source.IsMobile||scope.Unknown||!scope.WindowCurrent()||!scope.Source.CurrentContext()||scope.StationSync!=null)return false;
            if(!RebirthProductiveEarnedCoordinator.CanPrepareMobileRemoteTools(station,RebirthCamperRemoteStationGrid.Tools))return false;
            return scope.Steps==0?scope.OwnerBefore?.Saved(scope)==true:scope.Journal!=null;
        }
        catch{return false;}
    }
    internal static bool CanCommitMobileTools(XUiC_WorkstationWindowGroup window)
    {
        try
        {
            var station=window?.WorkstationData?.TileEntity as RebirthCamperStationState;
            return station!=null&&observations.TryGetValue(station,out var scope)&&ReferenceEquals(scope.Window,window)&&
                scope.Source.IsMobile&&scope.Journal!=null&&scope.StationSync==null&&scope.ReturnedContext()&&
                scope.OwnerAfter?.Saved(scope)==true&&
                RebirthProductiveEarnedCoordinator.CanPrepareMobileRemoteTools(station,RebirthCamperRemoteStationGrid.Tools);
        }
        catch{return false;}
    }
}
