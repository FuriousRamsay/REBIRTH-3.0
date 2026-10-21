using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    // The independently received real native Add must already be accepted.
    // The prefix cannot create that event or manufacture a viewer XP effect.
    private static bool HasActualDeferredOutputPrefix(RebirthWorkstationCompletionPipeline.NativeHostOriginal original,XElement contribution,XElement prefix)
    {
        try
        {
            if(original?.CurrentViewer()!=true||!original.AdmissionCurrent()||contribution?.Name!="deferredNativeContribution"||
                contribution.Attribute("phase")!=null||contribution.Element("physicalDelivery")!=null||contribution.Element("activeBefore")!=null||
                contribution.Element("actualCachedSpaceTransfer")!=null||!host.TryGetValue(original.Operation,out var accepted)||
                !ReferenceEquals(accepted.NativeOriginal,original)||accepted.NativePhase!=1||!accepted.SourceCurrentWithoutViewer()||
                !XNode.DeepEquals(accepted.NativeMembership,contribution)||
                !RebirthRemoteResourcePlacedStationNativeReader.TryReadCompletionArgument(ReadBytes(contribution,"nativeAddArgument"),out var argument))return false;
            var primary=RebirthProductiveEarnedUiRemote.ReadGrid(prefix,"primaryNative");
            return primary.Length==1&&NativeArgumentMatches(argument,original.Recipe,original.EarlierOwner.StartingEntityId)&&
                RebirthStationGridIngredients.IsSameStackSnapshot(primary[0],argument.CraftedItemStack);
        }
        catch{return false;}
    }
}
