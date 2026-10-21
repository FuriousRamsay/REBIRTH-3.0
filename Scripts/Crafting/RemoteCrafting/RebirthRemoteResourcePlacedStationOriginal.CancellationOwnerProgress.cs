using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    internal sealed class CancellationOwnerStep
    {
        internal Guid Key;internal int Ordinal,Slot;internal bool Bag,NativeReturned;
        internal string Before,After;internal byte[] Command;internal CancellationOwnerProgress Owner;
    }
    internal sealed class CancellationOwnerProgress
    {
        internal CancellationClient Original;internal string Permission,Plan,Before;internal byte[] PlanBytes;
        internal readonly List<CancellationOwnerStep> Steps=new List<CancellationOwnerStep>();
        internal int Completed;internal bool BaselineSaved;internal long ReadFloor;
        internal bool Current=>Original?.Memory?.Current==true&&RebirthRemoteResourcePlacedStationAllocation.HasScratch(Original.Memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope)&&Original.RestartSavedAfterOwner==null&&Original.Permission==Permission&&
            Original.NativeLease()&&Original.PermissionSaved&&Original.Actor.Buffs.GetCustomVar(CancellationReceipt(Original.Operation))==1f;
    }
    private static Guid OwnerProgressKey(Guid operation,int ordinal)
    {
        using(var sha=SHA256.Create())
        {var bytes=sha.ComputeHash(Encoding.UTF8.GetBytes(operation.ToString("N")+"|placed-cancellation-owner-slot-v1|"+ordinal));var id=new byte[16];Buffer.BlockCopy(bytes,0,id,0,16);return new Guid(id);}
    }
    private static bool OwnerProgressExact(CancellationOwnerProgress progress,RebirthCamperTransferJournal.Record record,Guid key,byte[] bytes,
        RebirthCamperTransferJournal.Phase phase)=>record!=null&&record.Operation==key&&record.Vehicle==progress.Original.Source&&record.OwnerCreation==progress.Original.Original.Creation&&
        record.Revision==0&&record.State==phase&&record.Before.SequenceEqual(bytes)&&record.After.SequenceEqual(bytes);
    private static RebirthRemoteResourceClientOriginalJournal OwnerProgressJournal(CancellationOwnerProgress progress,Guid key,string purpose,int size)
        =>CancellationJournal(key,purpose,size,progress.Original.Memory,progress.ReadFloor);
    private static bool WriteOwnerProgress(CancellationOwnerProgress progress,Guid key,string purpose,byte[] bytes,RebirthCamperTransferJournal.Phase phase)
    {
        if(!progress.Current||bytes==null||bytes.Length<1||bytes.Length>MaximumResultBytes||
            !progress.Original.Memory.Reserve(Math.Max(progress.Original.Memory.ReservedBytes,checked(progress.ReadFloor+32L*bytes.Length+65536))))return false;
        var journal=OwnerProgressJournal(progress,key,purpose,bytes.Length);var record=journal.Prepare(key,progress.Original.Source,progress.Original.Original.Creation,0,bytes,bytes);
        if(record.Operation!=key||record.Vehicle!=progress.Original.Source||record.OwnerCreation!=progress.Original.Original.Creation||record.Revision!=0||!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(phase!=RebirthCamperTransferJournal.Phase.Prepared&&record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(key,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(phase==RebirthCamperTransferJournal.Phase.Applied&&record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(key,record.State,RebirthCamperTransferJournal.Phase.Applied);
        return OwnerProgressExact(progress,record,key,bytes,phase);
    }
    internal static bool TryPrepareCancellationOwnerProgress(CancellationClient original,out CancellationOwnerProgress progress)
    {
        progress=original?.OwnerProgress;
        try
        {
            if(progress!=null)
            {
                if(!progress.Current)return false;
                if(!progress.BaselineSaved&&progress.Completed==0&&progress.Steps.All(value=>!value.NativeReturned)&&original.Before.Matches(RebirthCamperEnergyActorImage.Capture(original.Actor)))
                    progress.BaselineSaved=SaveCancellationOwnerCheckpoint(progress,null);
                return progress.BaselineSaved;
            }
            if(original==null||!RebirthRemoteResourcePlacedStationAllocation.HasScratch(original.Memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope)||!original.NativeLease()||!original.PermissionSaved||original.Permission==null||original.Before==null||original.After==null||
                original.Actor.Buffs.GetCustomVar(CancellationReceipt(original.Operation))!=1f||!original.Before.Matches(RebirthCamperEnergyActorImage.Capture(original.Actor)))return false;
            string before=RebirthRemoteResourceClientOriginal.ActorImage(original.Before),after=RebirthRemoteResourceClientOriginal.ActorImage(original.After);
            var changes=new List<Tuple<bool,int>>();
            foreach(bool bag in new[]{true,false})
            {
                var a=bag?original.Before.Bag:original.Before.Belt;var b=bag?original.After.Bag:original.After.Belt;
                if(a.Length!=b.Length)return false;
                for(int slot=0;slot<a.Length;slot++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(a[slot],b[slot]))
                {if(changes.Count>=32)return false;changes.Add(Tuple.Create(bag,slot));}
            }
            long workspace=checked(16L*(changes.Count+2)*(before.Length+after.Length+original.Permission.Length)+64L*1024*1024);
            if(!original.Memory.Reserve(checked(original.Memory.ReservedBytes+workspace)))return false;
            progress=new CancellationOwnerProgress{Original=original,Permission=original.Permission,Before=before};
            var prefix=original.Before.Copy();var entries=new XElement("slots");
            for(int i=0;i<changes.Count;i++)
            {
                var change=changes[i];var step=new CancellationOwnerStep{Key=OwnerProgressKey(original.Operation,i),Ordinal=i,Bag=change.Item1,Slot=change.Item2,Owner=progress,
                    Before=RebirthRemoteResourceClientOriginal.ActorImage(prefix)};
                if(step.Bag)prefix.Bag[step.Slot]=original.After.Bag[step.Slot].Clone();else prefix.Belt[step.Slot]=original.After.Belt[step.Slot].Clone();
                step.After=RebirthRemoteResourceClientOriginal.ActorImage(prefix);
                var entry=new XElement("slot",new XAttribute("ordinal",i),new XAttribute("key",step.Key.ToString("N")),new XAttribute("bag",step.Bag),new XAttribute("index",step.Slot),
                    new XElement("beforeActor",step.Before),new XElement("afterActor",step.After));entries.Add(entry);progress.Steps.Add(step);
            }
            if(RebirthRemoteResourceClientOriginal.ActorImage(prefix)!=after)return false;
            progress.Plan=new XElement("placedCancellationOwnerPlan",new XAttribute("version",1),new XAttribute("cancel",original.Operation.ToString("N")),
                new XAttribute("source",original.Source.ToString("N")),new XAttribute("binding",original.Binding),new XElement("permission",original.Permission),
                new XElement("originalBefore",before),new XElement("originalAfter",after),entries).ToString(SaveOptions.DisableFormatting);
            progress.PlanBytes=Encoding.UTF8.GetBytes(progress.Plan);if(progress.PlanBytes.Length>MaximumResultBytes/3)return false;
            progress.ReadFloor=original.Memory.ReservedBytes;
            foreach(var step in progress.Steps)
            {
                step.Command=Encoding.UTF8.GetBytes(new XElement("placedCancellationOwnerSlot",new XAttribute("version",1),new XAttribute("cancel",original.Operation.ToString("N")),
                    new XAttribute("key",step.Key.ToString("N")),new XAttribute("ordinal",step.Ordinal),new XElement("completePlan",progress.Plan),
                    new XElement("beforeActor",step.Before),new XElement("afterActor",step.After)).ToString(SaveOptions.DisableFormatting));
                if(!WriteOwnerProgress(progress,step.Key,"OwnerSlot",step.Command,RebirthCamperTransferJournal.Phase.Prepared))return false;
            }
            // This immutable Applied manifest is written only after EVERY slot
            // has a positive Prepared original, before any slot can enter.
            if(!WriteOwnerProgress(progress,original.Operation,"OwnerPlan",progress.PlanBytes,RebirthCamperTransferJournal.Phase.Applied))return false;
            original.OwnerProgress=progress;

            progress.BaselineSaved=SaveCancellationOwnerCheckpoint(progress,null);
            return progress.BaselineSaved;
        }
        catch{return false;}
    }
    internal static bool EnterCancellationOwnerStep(CancellationOwnerProgress progress,ItemStackGrid grid,int slot,out CancellationOwnerStep step)
    {
        step=null;try
        {
            if(progress?.Current!=true||!progress.BaselineSaved||progress.Completed>=progress.Steps.Count)return false;
            var next=progress.Steps[progress.Completed];var actor=progress.Original.Actor;
            if(next.Slot!=slot||!ReferenceEquals(grid,next.Bag?actor.bag.ItemGrid:actor.inventory.ItemGrid)||
                !RebirthRemoteResourceClientOriginal.ReadActor(next.Before).Matches(RebirthCamperEnergyActorImage.Capture(actor)))return false;
            var record=OwnerProgressJournal(progress,next.Key,"OwnerSlot",next.Command.Length).Read(next.Key);
            if(!OwnerProgressExact(progress,record,next.Key,next.Command,RebirthCamperTransferJournal.Phase.Prepared)||
                !WriteOwnerProgress(progress,next.Key,"OwnerSlot",next.Command,RebirthCamperTransferJournal.Phase.Uncertain))return false;
            step=next;return true;
        }catch{return false;}
    }
    internal static bool FinishCancellationOwnerStep(CancellationOwnerStep step)
    {
        try
        {
            var progress=step?.Owner;if(progress?.Current!=true||!step.NativeReturned||step.Ordinal!=progress.Completed||
                !RebirthRemoteResourceClientOriginal.ReadActor(step.After).Matches(RebirthCamperEnergyActorImage.Capture(progress.Original.Actor))||
                HoldCancellationScenario(progress.Original.Operation,"ownerReturned",step.Ordinal)||
                !SaveCancellationOwnerCheckpoint(progress,step))return false;
            if(HoldCancellationScenario(progress.Original.Operation,"ownerCheckpoint",step.Ordinal)||
                !WriteOwnerProgress(progress,step.Key,"OwnerSlot",step.Command,RebirthCamperTransferJournal.Phase.Applied))return false;
            progress.Completed++;return true;
        }catch{return false;}
    }
}
