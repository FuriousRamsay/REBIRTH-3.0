using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    private sealed class RecipientHandoffLease:IDisposable
    {
        internal IDisposable Source,Memory;
        public void Dispose(){Source?.Dispose();Memory?.Dispose();Source=Memory=null;}
    }
    internal static bool TryAcceptRecipientTerminal(RebirthProductiveEarnedUiDeferredTransport.ClientTerminalPublication terminal)
    {
        ObserveWorld();
        IDisposable source=null,memory=null;
        try
        {
            if(terminal?.Current()!=true||!terminal.Source.CurrentWindow()||terminal.Window?.craftingQueue==null||
                !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(terminal.Window,out long session)||session!=terminal.WindowSession)return false;
            var queue=terminal.Window.craftingQueue;
            if(handoffs.TryGetValue(queue,out var existing))return ReferenceEquals(existing.Binding.RecipientSource,terminal)&&existing.Current();
            var births=terminal.Source.Members.Select(value=>value.Birth).Distinct().ToArray();if(births.Length!=1||births[0]==Guid.Empty)return false;
            byte[] native,receipt;int offset;bool noChange;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                var cards=queue.GetRecipesToCraft();
                if(decoder==null||cards==null||cards.Any(card=>card.AmountToRepair!=0||card.OriginalItem!=null&&!card.OriginalItem.IsEmpty())||
                    !RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(terminal.Station,cards.Select(UiRow).ToArray(),
                        terminal.Window.inputWindow?.GetSlots(),terminal.Window.outputWindow?.GetSlots(),terminal.Window.fuelWindow?.GetSlots(),terminal.Window.toolWindow?.GetSlots(),
                        out native,out var projection,out offset,out int projected)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(native,offset,terminal.Body,terminal.Offset)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(native,offset,projection,projected))return false;
                // A positively final queue with no pending rotation already
                // has the exact native/UI relationship; no setter is invoked.
                noChange=cards.All(card=>!card.HasRecipe())||cards[^1].HasRecipe();
                receipt=File.ReadAllBytes(terminal.Path);
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*native.Length+4L*receipt.Length+65536),out memory))return false;
            }
            source=terminal.Source.Retain();if(source==null||!terminal.Current()||!terminal.Source.CurrentWindow())return false;
            var context=new QueueContext{RecipientSource=terminal,Actor=terminal.Actor,World=terminal.World,Station=terminal.Station,Window=terminal.Window,Queue=queue,
                Position=terminal.Station.ToWorldPos(),BlockType=terminal.World.GetBlock(terminal.Station.ToWorldPos()).type,Deaths=terminal.Source.Deaths,
                Owner=terminal.Source.Owner,Creation=terminal.Source.Creation,WorldKey=terminal.Source.WorldKey,
                SaveDirectory=Path.GetFullPath(GameIO.GetSaveGameDir()),PlayerDirectory=Path.GetFullPath(GameIO.GetPlayerDataDir()),Operation=terminal.Operation,Birth=births[0]};
            var retained=new RecipientHandoffLease{Source=source,Memory=memory};source=memory=null;
            var value=new QueueHandoff{Binding=context,Connection=terminal.Connection,WindowSession=session,Clock=terminal.Station.lastTickTime,
                TerminalPath=terminal.Path,TerminalReceipt=receipt,Native=native,Offset=offset,Path=terminal.Path+".queue.uqr",Retention=retained};
            if(!value.Current()){value.Dispose();return false;}
            handoffs.Add(queue,value);if(noChange&&!PrepareUnchangedRecipientQueue(value)){handoffs.Remove(queue);value.Dispose();return false;}return true;
        }
        catch{return false;}
        finally{source?.Dispose();memory?.Dispose();}
    }
}
