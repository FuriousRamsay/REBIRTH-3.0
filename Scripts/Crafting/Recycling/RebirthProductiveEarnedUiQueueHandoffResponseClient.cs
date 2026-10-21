using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    // This issuer accepts only the response to this retained, actually submitted
    // UQR. The authenticated envelope remains the sole native Process invocation.
    internal static bool TryBindQueueResponse(RebirthProductiveEarnedUiDeferredTransport.ClientHydration original,out object context)
    {
        context=null;var header=original?.Header?.Element("uqrOriginalResponse");if(header==null)return true;
        try
        {
            if(header.Attributes().Count()!=4||header.HasElements||(int?)header.Attribute("version")!=1)return false;
            var found=handoffs.Values.Where(value=>value.Submitted&&value.Observed&&value.Binding.Operation.ToString("N")== (string)header.Attribute("operation")&&
                value.Nonce.ToString("N")== (string)header.Attribute("nonce")&&Hash(value.Image)==(string)header.Attribute("digest")).ToArray();
            if(found.Length!=1)return false;var value=found[0];var b=value.Binding;
            if(!value.Current()||!HandoffUiCurrent(value)||!ReferenceEquals(original.World,b.World)||!ReferenceEquals(original.Station,b.Station)||
                !ReferenceEquals(original.Actor,b.Actor)||!ReferenceEquals(original.Connection,value.Connection)||original.Owner!=b.Owner||original.Creation!=b.Creation||original.Deaths!=b.Deaths)return false;
            context=value;return true;
        }
        catch{return false;}
    }
    internal static bool CanReplaceQueueResponseSource(object context,RebirthProductiveEarnedUiDeferredTransport.ClientHydration previous)
        =>context is QueueHandoff value&&ReferenceEquals(value.Binding.RecipientSource?.Source,previous)&&previous.HandoffAccepted&&
            previous.Consumed&&previous.OwnerStatus==5&&!previous.Unknown&&value.Current()&&HandoffUiCurrent(value);
    internal static bool IsActualQueueResponse(object context,NetPackageTileEntity packet,World world)
        =>context is QueueHandoff value&&value.Submitted&&value.Observed&&ReferenceEquals(value.Binding.World,world)&&
            value.Binding.Position.Equals(packet.teWorldPos)&&value.Binding.BlockType==packet.teBlockId&&value.Current()&&HandoffUiCurrent(value);
}
