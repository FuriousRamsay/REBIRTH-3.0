using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public static partial class RemoteResourceTransactions
{
    private sealed class ActionFreezeWorkspace:IDisposable
    {
        private const long Measurement=256L*RebirthNativeItemConformanceReader.MaximumNodes+528L*RebirthNativeItemConformanceReader.MaximumChildSlots+
            128L*(196608/5)+2L*RebirthNativeItemConformanceReader.MaximumAggregateStringBytes+196608L*4+65536+12L*1024*1024;
        private readonly List<IDisposable> allocations=new List<IDisposable>();private long retained,parser,reserved;private bool active=true;
        internal long Remaining=2L*1024*1024+65536;internal bool Active=>active;
        internal ActionFreezeWorkspace(){Reserve(Measurement);}
        private void Reserve(long bytes)
        {
            if(bytes<=reserved)return;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(bytes-reserved,out var allocation))throw new InvalidDataException("Electrical frozen native argument scratch unavailable");
            try{allocations.Add(allocation);reserved=bytes;allocation=null;}finally{allocation?.Dispose();}
        }
        internal void Admit(byte[] image,ItemStack planned)
        {
            if(!active||image==null||image.Length<17)throw new InvalidDataException();
            long graph,workspace;
            if(planned.itemValue.IsEmpty())
            {
                if(planned.count!=0||image.Length>512)throw new InvalidDataException("Unexpected empty native argument");
                graph=1024;workspace=65536;
            }
            else
            {
                if(!RebirthNativeItemConformanceReader.TryMeasureCanonicalV9PrefixStructure(image,16,out var measure,out int consumed)||consumed!=image.Length-16)
                    throw new InvalidDataException("Frozen native argument structure is unsupported");
                graph=checked(2*measure.CloneAllocationBound+image.Length+1024);workspace=measure.ParserScratchBound;
            }
            long nextRetained=checked(retained+graph),nextParser=Math.Max(parser,workspace);
            Reserve(checked(Measurement+nextRetained+nextParser));retained=nextRetained;parser=nextParser;
        }
        public void Dispose(){if(!active)return;active=false;foreach(var allocation in allocations)allocation.Dispose();allocations.Clear();}
    }
    // Native ItemValue.Clone shares TextureFullArray in the configured DLL.
    // Decode twice from immutable complete bytes so neither the passed argument
    // nor a native listener can mutate the independent expected graph.
    private sealed class ActionFrozenDesired:IDisposable
    {
        private readonly byte[] image;private readonly ActionFreezeWorkspace workspace;private readonly bool ownsWorkspace;private bool active=true;
        internal readonly ItemStack Argument,Expected;
        internal ActionFrozenDesired(ItemStack planned,ActionFreezeWorkspace shared=null)
        {
            workspace=shared??new ActionFreezeWorkspace();ownsWorkspace=shared==null;
            try
            {
                if(!workspace.Active)throw new InvalidDataException("Electrical freeze workspace retired");
                image=CaptureActionFrame(new[]{planned},ref workspace.Remaining);
                workspace.Admit(image,planned);
                Argument=RebirthCamperInventorySnapshot.Restore(image)[0];Expected=RebirthCamperInventorySnapshot.Restore(image)[0];
            }
            catch{if(ownsWorkspace)workspace.Dispose();throw;}
        }
        internal bool Matches(ItemStack value)
        {
            try{if(!active||!workspace.Active)return false;long remaining=2L*1024*1024;return CaptureActionFrame(new[]{value},ref remaining).SequenceEqual(image);}catch{return false;}
        }
        public void Dispose(){active=false;if(ownsWorkspace)workspace.Dispose();}
    }
}
