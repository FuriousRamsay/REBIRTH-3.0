using System;
using System.Linq;
using System.Xml.Linq;

// Authored, NOT RUN. Golden cells must come from an independently observed
// actual native ChangeBlocks on a disposable world, including child density.
// This fixture does not provide payment, birth, invocation or save authority.
internal static class RemoteBlockFootprintChecks
{
    internal static void SameValueRepair(World actualWorld,Vector3i parent,BlockValue actualBefore,BlockValue independentlyObservedAfter)
    {
        if(actualBefore.rawData!=independentlyObservedAfter.rawData||actualBefore.damage<=independentlyObservedAfter.damage)
            throw new Exception("fixture needs actual unchanged raw repair and positive damage decrease");
        if(!RebirthRemoteResourceBlockFootprint.TryPlan(actualWorld,parent,actualBefore,independentlyObservedAfter,out var cells)||cells.Length!=0)
            throw new Exception("same-value native repair incorrectly requires add/remove callback footprint");
    }
    internal static void Run(World preparedWorld,Vector3i parent,BlockValue before,BlockValue after,
        Vector3i[] nativePositions,uint[] nativeRaw,int[] nativeDamage,sbyte[] nativeDensity)
    {
        if(!RebirthRemoteResourceBlockFootprint.TryPlan(preparedWorld,parent,before,after,out var planned)||planned.Length!=nativePositions.Length)
            throw new Exception("native footprint count");
        for(int index=0;index<nativePositions.Length;index++)
        {
            var cell=planned.SingleOrDefault(value=>value.Position==nativePositions[index]);
            if(cell==null||cell.After.rawData!=nativeRaw[index]||cell.After.damage!=nativeDamage[index]||cell.AfterDensity!=nativeDensity[index])
                throw new Exception("independently observed native child postimage");
        }
        string encoded=RebirthRemoteResourceBlockFootprint.Encode(planned);
        if(!RebirthRemoteResourceBlockFootprint.TryDecode(encoded,out var copy)||RebirthRemoteResourceBlockFootprint.Encode(copy)!=encoded)
            throw new Exception("canonical footprint");
        var extra=XElement.Parse(encoded);extra.SetAttributeValue("unknown",1);
        if(RebirthRemoteResourceBlockFootprint.TryDecode(extra.ToString(SaveOptions.DisableFormatting),out _))throw new Exception("unknown field accepted");
        var duplicate=XElement.Parse(encoded);if(duplicate.Elements("cell").Any())
        {
            duplicate.Add(new XElement(duplicate.Elements("cell").First()));
            if(RebirthRemoteResourceBlockFootprint.TryDecode(duplicate.ToString(SaveOptions.DisableFormatting),out _))throw new Exception("duplicate child accepted");
        }
    }
    // Required native vectors: rotation crossing a chunk border; overlapping
    // old/new children; TYPE-only removal of a same-type nonchild; terrain
    // addition skip; POI add suppression versus rotation callback; child y0/253;
    // unloaded child, tile/power-node collision, >15 union, changed parent/child
    // birth, same raw/density ABA and interrupted per-cell saved admission.
    // Same-value vectors include base value-change containers with overridden
    // add/remove callbacks and native BladeTrap (PoweredTrap value callback
    // refreshes visual state with switch=false). Unknown value-change
    // override and type/rotation transitions must not inherit that shortcut.
    // Full MP host acceptance additionally needs all native post-publication,
    // owner2/root2 and once-only original command; these are not doubled here.
}
