using System.Linq;

internal static partial class RebirthProductiveEarnedUiOwnerFile
{
    internal static bool IsExecutingCachedOwnerConsumption(TileEntityWorkstation station,EntityPlayerLocal actor,RebirthWorkstationCompletionPipeline.NativeAddOriginal original)
        =>original!=null&&ThreadManager.IsMainThread()&&localOwners.Values.Any(value=>value.Invoking&&value.Attempted&&value.BeforeSaved&&value.BeforePublished&&
            !value.Unknown&&ReferenceEquals(value.Station,station)&&ReferenceEquals(value.Actor,actor)&&value.CurrentWindow()&&value.BodyCurrent(false)&&
            value.Members.Any(member=>ReferenceEquals(member.NativeOriginal,original)));
    internal static bool TryGetCachedOwnerPublication(RebirthWorkstationCompletionPipeline.NativeAddOriginal original,out LocalOwnerTerminalPublication proof)
    {
        proof=null;if(original==null||!ThreadManager.IsMainThread())return false;
        var matches=localOwners.Values.Where(value=>!value.Closed&&value.Members.Any(member=>ReferenceEquals(member.NativeOriginal,original))&&
            value.Terminal?.CurrentOwnerEvent()==true&&value.BodyCurrent(true)).ToArray();
        if(matches.Length!=1)return false;proof=matches[0].Terminal;return true;
    }
}
