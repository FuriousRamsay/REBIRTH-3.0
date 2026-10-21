using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    internal static bool TryAcceptLocalOwnerTerminal(RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication terminal)
    {
        ObserveWorld();
        IDisposable source=null,memory=null;
        try
        {
            var scope=terminal?.Scope;
            if(terminal?.Current()!=true||scope?.TerminalAccepted!=true||terminal.World.IsRemote()||!scope.CurrentWindow()||scope.Window?.craftingQueue==null||
                !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(scope.Window,out long session)||session!=scope.WindowSession)return false;
            // The owner callback may have completed while native output was
            // full. Its saved event is not the physical UIO terminal.
            if(scope.Members.Any(member=>member.Original!=null&&(!member.Original.Delivered||!member.Original.TerminalSaved)))return false;
            var queue=scope.Window.craftingQueue;
            if(handoffs.TryGetValue(queue,out var existing))return ReferenceEquals(existing.Binding.LocalOwnerSource,terminal)&&existing.Current();
            var births=scope.Members.Select(value=>value.EarlierOwner.Birth).Distinct().ToArray();if(births.Length!=1||births[0]==Guid.Empty)return false;
            byte[] native,receipt;int offset;bool empty=false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                var cards=queue.GetRecipesToCraft();
                if(decoder==null||cards==null||cards.Any(card=>card.AmountToRepair!=0||card.OriginalItem!=null&&!card.OriginalItem.IsEmpty())||
                    !RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(terminal.Station,cards.Select(UiRow).ToArray(),
                        scope.Window.inputWindow?.GetSlots(),scope.Window.outputWindow?.GetSlots(),scope.Window.fuelWindow?.GetSlots(),scope.Window.toolWindow?.GetSlots(),
                        out native,out var projection,out offset,out int projected)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(native,offset,terminal.Body,terminal.Offset)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(native,offset,projection,projected))return false;
                empty=cards.All(card=>!card.HasRecipe());
                if(!empty&&cards[^1].HasRecipe())return RetainUnchangedLocalQueue(terminal,native,offset);
                var file=new FileInfo(terminal.Path);if(file.Length<1||file.Length>24L*1024*1024)return false;
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*native.Length+4L*file.Length+65536),out memory))return false;
                receipt=File.ReadAllBytes(terminal.Path);if(receipt.Length!=file.Length)return false;
            }
            source=scope.Retain();if(source==null||!terminal.Current()||!scope.CurrentWindow())return false;
            var context=new QueueContext{LocalOwnerSource=terminal,Actor=terminal.Actor,World=terminal.World,Station=terminal.Station,Window=scope.Window,Queue=queue,
                Position=terminal.Station.ToWorldPos(),BlockType=terminal.World.GetBlock(terminal.Station.ToWorldPos()).type,Deaths=scope.Deaths,
                Owner=scope.Owner,Creation=scope.Creation,WorldKey=RebirthProductiveEarnedArtifact.WorldIdentity(terminal.World),
                SaveDirectory=Path.GetFullPath(GameIO.GetSaveGameDir()),PlayerDirectory=Path.GetFullPath(GameIO.GetPlayerDataDir()),Operation=terminal.Operation,Birth=births[0]};
            var retained=new RecipientHandoffLease{Source=source,Memory=memory};source=memory=null;
            var value=new QueueHandoff{Binding=context,WindowSession=session,Clock=terminal.Station.lastTickTime,TerminalPath=terminal.Path,
                TerminalReceipt=receipt,Native=native,Offset=offset,Path=terminal.Path+".queue.uqr",Retention=retained};
            if(!value.Current()||empty&&!PrepareEmptyLocalQueue(value)){value.Dispose();return false;}handoffs.Add(queue,value);return true;
        }
        catch{return false;}
        finally{source?.Dispose();memory?.Dispose();}
    }
}

