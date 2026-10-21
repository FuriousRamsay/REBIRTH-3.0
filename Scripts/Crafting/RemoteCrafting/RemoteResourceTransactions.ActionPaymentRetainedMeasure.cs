using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    private sealed class ActionAllocationReferences:IEqualityComparer<object>
    {public new bool Equals(object left,object right)=>ReferenceEquals(left,right);public int GetHashCode(object value)=>RuntimeHelpers.GetHashCode(value);}
    private static bool NarrowActionPaymentMemory(ActionPayment payment)
    {
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var measurement))return false;
        using(measurement)
        {
            var seen=new HashSet<object>(new ActionAllocationReferences());
            long Items(ItemStack[] values)=>values==null||!seen.Add(values)?0:MeasureActionRetainedItems(values);
            long Text(string value)=>value==null||!seen.Add(value)?0:checked(32L+2L*value.Length);
            long Snapshot(RebirthNpcInventorySnapshot snapshot)
            {
                if(snapshot==null||!seen.Add(snapshot))return 0;
                long bytes=256;
                foreach(var map in new[]{snapshot.Quantities,snapshot.Reservations})
                    if(map!=null&&seen.Add(map)){bytes=checked(bytes+128L*map.Count+128);foreach(var key in map.Keys)bytes=checked(bytes+Text(key));}
                if(snapshot.LogisticsEffects!=null&&seen.Add(snapshot.LogisticsEffects))
                {bytes=checked(bytes+192L*snapshot.LogisticsEffects.Count+128);foreach(var value in snapshot.LogisticsEffects.Values)bytes=checked(bytes+Text(value));}
                if(snapshot.NativeStacks!=null&&seen.Add(snapshot.NativeStacks))
                {
                    var xml=snapshot.NativeStacks.Write();int nodes=0;
                    foreach(var node in xml.DescendantsAndSelf())
                    {
                        nodes++;bytes=checked(bytes+160);
                        foreach(var attribute in node.Attributes())bytes=checked(bytes+96+Text(attribute.Value));
                        foreach(var child in node.Nodes())if(child is XText text)bytes=checked(bytes+64+Text(text.Value));
                    }
                    // Immutable NativeStackSet additionally indexes quantities.
                    bytes=checked(bytes+128L*nodes);
                }
                return bytes;
            }
            long total=checked(65536L+2*payment.OriginalBytesBound+Items(payment.OwnerBagBefore)+Items(payment.OwnerBeltBefore));
            foreach(var leg in payment.Legs)
            {
                long sourceRemaining=2L*1024*1024;long sourceFrameBytes=CaptureActionFrame(leg.Before,ref sourceRemaining).LongLength;
                // A native not-run/never-entered boundary can retain one full
                // progress refusal per participant. Its native item image never
                // exceeds this original Before (only counts/empty rows change).
                long refusal=checked(2*((sourceFrameBytes+2)/3*4)+4096);
                total=checked(total+4096+Text(leg.Id)+Items(leg.Before)+Items(leg.After)+Items(leg.Progress)+Items(leg.Paid)+
                    refusal+leg.Locks.LongLength+16L*leg.Slots.Length+Snapshot(leg.NpcBefore)+Snapshot(leg.NpcAfter)+(leg.NpcBodyBefore?.LongLength??0));
            }
            // Native saved artifacts/owner phases and native publication watches
            // retain separate measured leases; do not charge their buffers here.
            return RebirthProductiveEarnedObservationBudget.TryResizeDetached(payment.Memory,total);
        }
    }
    // Shared codec primitives only: these confer no owning-action authority.
    internal static byte[] CaptureOriginalNativeFrame(ItemStack[] items,ref long remaining)=>CaptureActionFrame(items,ref remaining);
    internal static long MeasureOriginalNativeFrame(byte[] image)=>MeasureActionRetainedFrame(image);
    private static long MeasureActionRetainedItems(ItemStack[] items)
    {
        if(items==null)return 0;
        long remaining=2L*1024*1024+65536;byte[] frame=CaptureActionFrame(items,ref remaining);
        return MeasureActionRetainedFrame(frame,items);
    }
    private static long MeasureActionRetainedFrame(byte[] frame,ItemStack[] items)
    {
        return MeasureActionRetainedFrame(frame,items.Length);
    }
    private static long MeasureActionRetainedFrame(byte[] frame,int expectedCount=-1)
    {
        long emptyRemaining=1024;byte[] empty=CaptureActionFrame(new[]{ItemStack.Empty.Clone()},ref emptyRemaining);
        using(var input=new MemoryStream(frame,false))using(var reader=new BinaryReader(input))
        {
            if(reader.ReadUInt32()!=0x31534352)throw new InvalidDataException();int rows=reader.ReadInt32();
            if(rows<1||rows>short.MaxValue||expectedCount>=0&&rows!=expectedCount||rows>(input.Length-input.Position)/8)throw new InvalidDataException();
            long graph=checked(64L+544L*rows);
            for(int slot=0;slot<rows;slot++)
            {
                int count=reader.ReadInt32(),length=reader.ReadInt32(),start=checked((int)input.Position);
                if(length<1||length>input.Length-start||count<0)throw new InvalidDataException();
                if(RebirthNativeItemConformanceReader.TryMeasureCanonicalV9PrefixStructure(frame,start,out var measure,out int consumed)&&consumed==length)
                    graph=checked(graph+measure.CloneAllocationBound);
                else
                {
                    if(count!=0||length!=empty.Length-16)throw new InvalidDataException();
                    for(int index=0;index<length;index++)if(frame[start+index]!=empty[16+index])throw new InvalidDataException();
                    graph=checked(graph+512);
                }
                input.Position=checked(start+length);
            }
            if(input.Position!=input.Length)throw new InvalidDataException();
            return graph;
        }
    }
    private static bool NarrowActionRemoteClickMemory(ActionRemoteClick click)
    {
        // Called while the existing envelope scratch still covers measurement
        // and serialization temporaries. Each surviving owned frame/body is
        // charged once; native world/grid objects are observed references.
        long bytes=checked(65536L+MeasureActionRetainedItems(click.BagBefore)+MeasureActionRetainedItems(click.BeltBefore)+MeasureActionRetainedItems(click.Price)+
            8L*(click.BagReferences.Length+click.BeltReferences.Length)+2L*(click.Original.Length+click.Account.Length+click.Creation.Length+click.NativeWorld.Length)+
            (click.NativeBefore?.LongLength??0)+(click.RequestBody?.Length??0));
        return RebirthProductiveEarnedObservationBudget.TryResizeDetached(click.Memory,bytes);
    }
    private static long MeasureActionRemotePlanMemory(ActionRemoteDebitPlan plan)=>checked(65536L+2L*plan.Original.Length+plan.HostNativeBefore.LongLength+
        MeasureActionRetainedItems(plan.BagAfter)+MeasureActionRetainedItems(plan.BeltAfter)+64L*plan.Takes.Count);
    private static bool NarrowActionRemoteHostMemory(ActionRemoteHostOriginal source)
    {
        if(source.ProjectionGraphBytes==0)source.ProjectionGraphBytes=checked(MeasureActionRetainedItems(source.BagBefore)+MeasureActionRetainedItems(source.BeltBefore)+MeasureActionRetainedItems(source.Price));
        long bytes=checked(65536L+source.ProjectionGraphBytes+2L*(source.Click.Length+source.Account.Length+source.Creation.Length+source.NativeWorld.Length+source.Save.Length+source.Path.Length+source.Directory.Length)+
            2L*(source.NoAdmission?.Length??0)+(source.NativeBefore?.LongLength??0)+(source.NativeTerminal?.LongLength??0));
        return RebirthProductiveEarnedObservationBudget.TryResizeDetached(source.Memory,bytes);
    }
    private static bool CaptureActionRemoteHostImage(ActionRemoteHostOriginal source,PlayerDataFile data,bool terminal)
    {
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var transfer))return false;
        using(transfer)
        {
            var previous=terminal?source.NativeTerminal:source.NativeBefore;var native=ActionOwnerImage(data);
            if(terminal)source.NativeTerminal=native;else source.NativeBefore=native;
            if(NarrowActionRemoteHostMemory(source))return true;
            if(terminal)source.NativeTerminal=previous;else source.NativeBefore=previous;return false;
        }
    }
}
