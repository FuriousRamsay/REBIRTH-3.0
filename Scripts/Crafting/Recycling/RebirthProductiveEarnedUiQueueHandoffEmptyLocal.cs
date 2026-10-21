using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    private static bool EmptyLocalQueue(QueueHandoff value)
    {
        var terminal=value.Binding.LocalOwnerSource;var scope=terminal?.Scope;var cards=value.Binding.Queue.GetRecipesToCraft();
        return terminal?.CurrentOwnerEvent()==true&&scope.TerminalAccepted&&scope.CurrentWindow()&&scope.CachedPending==null&&
            scope.Members.All(member=>member.Delivered&&member.Consumed&&member.Completed&&
                (member.Original==null||member.Original.Delivered&&member.Original.TerminalSaved)&&
                (member.NativeOriginal==null||member.OwnerTerminalSaved))&&cards!=null&&
            cards.All(card=>card.GetRecipe()==null&&card.GetRecipeCount()==0&&!card.isInventoryFull&&
                card.AmountToRepair==0&&(card.OriginalItem==null||card.OriginalItem.IsEmpty()))&&
            value.Binding.Station.Queue!=null&&value.Binding.Station.Queue.Length==cards.Length&&
            value.Binding.Station.Queue.All(row=>row!=null&&row.Recipe==null&&row.Multiplier==0&&row.AmountToRepair==0&&(row.RepairItem==null||row.RepairItem.IsEmpty()));
    }
    private static bool PrepareEmptyLocalQueue(QueueHandoff value)
    {
        if(value.Binding.LocalOwnerSource?.Current()!=true)return false;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||value.Binding.World.IsRemote()||!value.Current()||!EmptyLocalQueue(value)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(value.Binding.Station,value.Native,value.Offset))return false;
            var cards=value.Binding.Queue.GetRecipesToCraft();value.Cards=cards;
            value.Rows=cards.Select(card=>RebirthProductiveEarnedRowProof.CaptureRow(UiRow(card))).ToArray();
            value.Definitions=cards.Select(_=>string.Empty).ToArray();value.Grids=HandoffGrids(value);
            if(!RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(value.Binding.Station,cards.Select(UiRow).ToArray(),
                value.Binding.Window.inputWindow?.GetSlots(),value.Binding.Window.outputWindow?.GetSlots(),value.Binding.Window.fuelWindow?.GetSlots(),value.Binding.Window.toolWindow?.GetSlots(),
                out var before,out value.After,out int offset,out value.AfterOffset)||
                !RebirthProductiveEarnedNativeBody.SameExceptElapsed(value.Native,value.Offset,before,offset)||
                !RebirthProductiveEarnedNativeBody.SameExceptElapsed(value.Native,value.Offset,value.After,value.AfterOffset))return false;
            var image=new XElement("nativeUiEmptyLocalQueueHandoff",new XAttribute("version",1),new XAttribute("operation",value.Binding.Operation.ToString("N")),
                new XAttribute("terminal",Hash(value.TerminalReceipt)),new XAttribute("birth",value.Binding.Birth.ToString("N")),new XAttribute("shifts",0),
                new XAttribute("beforeOffset",value.Offset),new XAttribute("afterOffset",value.AfterOffset),new XElement("before",Convert.ToBase64String(value.Native)),new XElement("after",Convert.ToBase64String(value.After)));
            value.Image=Encoding.UTF8.GetBytes(image.ToString(SaveOptions.DisableFormatting));value.EmptyLocal=true;
            return WriteExact(value.Path,value.Image);
        }
    }
    internal static bool IsExecutingEmptyLocalQueueSync(XUiC_WorkstationWindowGroup window)=>handoffSync is QueueHandoff value&&value.EmptyLocal&&
        ReferenceEquals(value.Binding.Window,window)&&value.Attempted&&!value.Entered&&value.Current()&&value.Binding.LocalOwnerSource.Current()&&HandoffUiCurrent(value)&&EmptyLocalQueue(value);

    // This token releases ONLY the settled graph. It does not admit a future
    // queue owner or restore any custody after the observed empty transition.
    internal sealed class SavedEmptyLocalQueue
    {
        private readonly QueueHandoff handoff;
        private SavedEmptyLocalQueue(QueueHandoff value){handoff=value;}
        internal RebirthProductiveEarnedUiOwnerFile.LocalOwnerScope PreviousScope=>handoff.Binding.LocalOwnerSource.Scope;
        internal RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication PreviousProof=>handoff.Binding.LocalOwnerSource;
        internal World World=>handoff.Binding.World;
        internal TileEntityWorkstation Station=>handoff.Binding.Station;
        internal Guid Group=>handoff.Binding.Operation;
        internal string Path=>handoff.Path;
        internal long MeasureImage()=>handoff.Image?.LongLength??0;
        internal byte[] CopyImage()=>handoff.Image==null?null:(byte[])handoff.Image.Clone();
        internal bool Current()=>ThreadManager.IsMainThread()&&!World.IsRemote()&&handoff.EmptyLocal&&handoff.Attempted&&handoff.Entered&&handoff.Observed&&
            handoffs.TryGetValue(handoff.Binding.Queue,out var source)&&ReferenceEquals(source,handoff)&&handoff.Current()&&HandoffUiCurrent(handoff)&&
            PreviousScope.HandoffAccepted&&PreviousProof.Current()&&EmptyLocalQueue(handoff)&&
            RebirthProductiveEarnedArtifact.FileMatches(handoff.Path,handoff.Image)&&
            RebirthProductiveEarnedArtifact.FileMatches(handoff.Path+".attempt",Encoding.ASCII.GetBytes("UQR1-attempt:"+Hash(handoff.Image)))&&
            RebirthProductiveEarnedArtifact.FileMatches(handoff.Path+".observed",Encoding.ASCII.GetBytes("UQR1-observed:"+Hash(handoff.Image)))&&
            RebirthProductiveEarnedArtifact.FileMatches(handoff.Path+".accepted",Encoding.ASCII.GetBytes("UQR1-local-sync:"+Hash(handoff.Image)));
        internal static SavedEmptyLocalQueue TryIssue(object source)
        {if(source is not QueueHandoff actual)return null;var value=new SavedEmptyLocalQueue(actual);return value.Current()?value:null;}
    }
    private static void FinishEmptyLocalQueue(QueueHandoff value)
    {
        if(!value.EmptyLocal||!value.Observed||!value.Current()||!value.Binding.LocalOwnerSource.Current()||!HandoffUiCurrent(value))return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            if(decoder==null||!EmptyLocalQueue(value)||!RebirthProductiveEarnedNativeBody.MatchesCurrent(value.Binding.Station,value.After,value.AfterOffset))return;
        if(!WriteExact(value.Path+".accepted",Encoding.ASCII.GetBytes("UQR1-local-sync:"+Hash(value.Image))))return;
        var proof=SavedEmptyLocalQueue.TryIssue(value);
        if(proof==null||!RebirthProductiveEarnedUiOwnerFile.ReleaseEmptyLocalOwnerQueueAuthority(proof))return;
        handoffs.Remove(value.Binding.Queue);value.Dispose();
    }
}
