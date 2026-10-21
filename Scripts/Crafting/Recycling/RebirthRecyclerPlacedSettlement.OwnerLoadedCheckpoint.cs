using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed class LoadedOwnerCheckpoint:IDisposable
    {
        internal readonly Original Original;internal readonly int Phase,Completed;
        private readonly string path;private byte[] receipt;private IDisposable memory;
        private LoadedOwnerCheckpoint(Original original,int phase,int completed,string path,byte[] receipt,IDisposable memory)
        {Original=original;Phase=phase;Completed=completed;this.path=path;this.receipt=receipt;this.memory=memory;}
        internal bool ObservedSaved()=>memory!=null&&Original.Current()&&Original.Actor.Buffs.GetCustomVar(Original.Receipt)==Phase&&
            Original.Actor.Buffs.GetCustomVar(Original.Checkpoint)==Completed&&
            Original.MatchesOwnerCheckpoint(Original.Actor.bag.ItemGrid.items,Original.Actor.inventory.ItemGrid.items,Completed)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,receipt);
        internal bool ObservedSavedCustody()=>memory!=null&&receipt!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,receipt);
        internal XElement CopyEvidence()=>ObservedSaved()?XElement.Parse(Encoding.UTF8.GetString(receipt)):null;
        internal static LoadedOwnerCheckpoint FromActualLoad(LoadedOwner loaded,Original original,int phase,int completed)
        {
            if(loaded?.MatchesOriginalCheckpoint(original,phase,completed)!=true||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*(loaded.native.Length+original.Image.Length)+65536),out var memory))return null;
            try
            {
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("world",original.WorldKey),
                    new XElement("before",XElement.Parse(Encoding.UTF8.GetString(original.Image))),new XElement("recyclerNativeOwnerLoaded",
                        new XAttribute("phase",phase),new XAttribute("completed",completed),new XAttribute("path",loaded.path),
                        new XElement("native",Convert.ToBase64String(loaded.native))));
                string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),original.Operation.ToString("N")+".owner-loaded."+phase+"."+completed+".xml");
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||
                    !loaded.MatchesOriginalCheckpoint(original,phase,completed))return null;
                byte[] receipt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(receipt==null)return null;
                var result=new LoadedOwnerCheckpoint(original,phase,completed,path,receipt,memory);memory=null;return result;
            }
            finally{memory?.Dispose();}
        }
        public void Dispose(){receipt=null;memory?.Dispose();memory=null;}
    }
    private static bool TryClassifyLoadedOwner(LoadedOwner loaded,LoadedStation station,Original original,out int phase,out int completed,out bool observedSlot)
    {
        phase=completed=0;observedSlot=false;
        if(loaded?.Current()!=true||station?.Current()!=true||original?.Current()!=true||
            !PhaseBytes(loaded.buffs,original.Receipt,out float nativePhase)||!PhaseBytes(loaded.buffs,original.Checkpoint,out float nativeCompleted)||
            nativePhase!=(int)nativePhase||nativeCompleted!=(int)nativeCompleted)return false;
        phase=(int)nativePhase;completed=(int)nativeCompleted;
        if(phase==0&&completed==0&&station.Stage=="before"&&original.MatchesOwnerCheckpoint(loaded.bag,loaded.belt,0)&&
            loaded.actor.Buffs.GetCustomVar(original.Receipt)==0f&&loaded.actor.Buffs.GetCustomVar(original.Checkpoint)==0f)
            return !HasUnresolvedOwnerSlotAttempt(original,0);
        if(!ValidOwnerCheckpointStage(phase,completed,original.Slots.Length)||
            loaded.actor.Buffs.GetCustomVar(original.Receipt)!=nativePhase||loaded.actor.Buffs.GetCustomVar(original.Checkpoint)!=nativeCompleted)return false;
        if(loaded.MatchesOriginalCheckpoint(original,phase,completed))return !HasUnresolvedOwnerSlotAttempt(original,completed)&&
            (station.Stage=="after"||phase==1&&completed==0);
        // A save from inside the native SlotChanged callback can contain the
        // complete next slot while retaining its prior stamp. This actual
        // primary read plus exact WAL membership proves that persisted slot;
        // no setter is repeated and arbitrary mixed images are refused.
        if(station.Stage!="after"||phase==3||completed>=original.Slots.Length||
            !original.MatchesOwnerCheckpoint(loaded.bag,loaded.belt,completed+1))return false;
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),original.Operation.ToString("N")+".slot."+completed+".attempted.xml");
        var attempt=RebirthWorkstationCompletionPublication.ReadPrimary(path);var slot=original.Slots[completed];
        var actual=attempt?.Element("recyclerOwnerPublication")?.Element("slotAttempt");
        if(attempt?.Name!="workstationCompletionIntent"||(string)attempt.Attribute("operation")!=original.Operation.ToString("N")||
            (string)attempt.Attribute("world")!=original.WorldKey||!XNode.DeepEquals(attempt.Element("before")?.Elements().SingleOrDefault(),XElement.Parse(Encoding.UTF8.GetString(original.Image)))||
            actual==null||actual.HasElements||actual.Nodes().Any()||actual.Attributes().Count()!=3||(int?)actual.Attribute("ordinal")!=completed||
            (int?)actual.Attribute("area")!=slot.Area||(int?)actual.Attribute("slot")!=slot.Index)return false;
        completed++;observedSlot=true;return true;
    }
}
