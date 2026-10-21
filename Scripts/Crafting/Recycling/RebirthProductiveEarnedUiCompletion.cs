using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    internal static bool BeforeUpdate(XUiC_RecipeStack card,out object state)
    {
        state=null;if(!RebirthProductiveEarnedUiOriginal.Enabled)return true;
        if(!originals.TryGetValue(card,out var original))
            return !Blocks(card?.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity);
        try
        {
            if(!original.PreparationComplete)
            {ContinuePreparation(original,card);return false;}
            if(original.Recovered||original.Ui==null||!original.Current()||original.Delivered||!original.Ui.OwnerInventoryCurrent())return false;
            if(!original.WearAttempted)return true; // Actual final SetTime owns preparation/wear.
            if(!original.Worn)FinishObservedWear(original);
            if(!original.Worn)return false;
            if(!original.EarnedSaved)
            {
                if(original.Binding.World.IsRemote())
                {
                    if(!File.Exists(original.Path+".AfterUploadAttempted")&&original.Ui.CurrentEarnedProjection()&&Stage(original,"AfterUploadAttempted"))UploadUi(original);
                    if(RebirthProductiveEarnedWire.TryReadReply(original.Binding.Operation,out var reply)&&reply.Operation==original.Binding.Operation&&reply.Nonce==original.Nonce&&
                        reply.Digest==original.Digest&&reply.State==RebirthProductiveEarnedWire.Status.Published&&reply.Birth==original.Binding.Birth&&
                        reply.Owner==original.Binding.Owner&&reply.Creation==original.Binding.Creation&&reply.Deaths==original.Binding.Deaths&&
                        reply.World==original.Binding.WorldKey&&reply.Position.Equals(original.Binding.Station.ToWorldPos())&&Stage(original,"EarnedSaved"))original.EarnedSaved=true;
                    else QueryUi(original);
                }
                else
                {
                    if(original.After==null)
                    {
                        RebirthProductiveEarnedRowProof.Frame frame;
                        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder()){if(decoder==null)return false;frame=original.Ui.ReadFrame();}
                        original.After=BeginPublication(original,"earned",frame.AfterNative,frame.AfterElapsedOffset);
                    }
                    if(original.After?.Saved()==true&&Stage(original,"EarnedSaved")){original.EarnedSaved=true;original.After.Dispose();original.After=null;}
                }
                if(!original.EarnedSaved)return false;
            }
            if(!original.CompletionAttempted)
            {
                if(!original.Ui.CurrentEarnedProjection()||!RebirthRecipeOutputIntegration.CanFitOwningCompletion(card,card.isInventoryFull)||
                    !Stage(original,"CompletionAttempted"))return false;
                original.CompletionAttempted=true;
            }
            if(RebirthWorkstationCompletionPipeline.HandlesOwningCard(card))return true;
            if(original.Output==null)original.Output=RebirthRecipeOutputIntegration.BeginOwningObservation(card,card.isInventoryFull,original.Binding.Operation);
            else if(original.Output.RanUpdate)
            {
                if(!RebirthRecipeOutputIntegration.TryContinueOwningCached(original.Output,out var successor))return false;
                original.Output=successor;
                if(!successor.TryCopyBeforeEvidence(out var continuation)||!WriteExact(original.Path+".cached."+successor.NativeVersionBefore,
                    System.Text.Encoding.UTF8.GetBytes(continuation.ToString(SaveOptions.DisableFormatting))))return false;
                if(!RebirthRecipeOutputIntegration.ResumeOwningObservation(successor))return false;
            }
            else if(!RebirthRecipeOutputIntegration.ResumeOwningObservation(original.Output))return false;
            if(original.Output==null)return false;state=original;return true;
        }
        catch{return false;}
    }
    internal static void AfterUpdate(XUiC_RecipeStack card,bool ran,object state)
    {
        if(state is not Original original||original.Output==null)return;
        RebirthRecipeOutputIntegration.ObserveOwningUpdate(card,ran,original.Output);
        if(RebirthRecipeOutputIntegration.TryTakeOwningDelivery(original.Output,out var delivery))
        {try{ObserveOwningDelivery(delivery);}finally{delivery.Dispose();}}
    }
    internal static void EndUpdate(XUiC_RecipeStack card,bool ran,object state)
    {
        if(state is not Original original||original.Output==null)return;
        RebirthRecipeOutputIntegration.NoteOwningUpdateExit(card,ran,original.Output);
        RebirthRecipeOutputIntegration.EndOwningObservation(original.Output);
    }
    internal static void ObserveOwningDelivery(RebirthRecipeOutputIntegration.OwningDelivery delivery)
    {
        try
        {
            if(delivery==null||!originals.TryGetValue(delivery.Card,out var original)||original.Delivered||!original.CompletionAttempted||
                !original.EarnedSaved||delivery.Operation!=original.Binding.Operation||!ReferenceEquals(delivery.Viewer,original.Binding.Actor)||
                !ReferenceEquals(delivery.World,original.Binding.World)||!ReferenceEquals(delivery.Station,original.Binding.Station)||
                !delivery.TryCopyEvidence(out var evidence)||original.OwnerFile?.ObserveDelivery(delivery)!=true)return;
            if(original.Deferred!=null&&!original.Deferred.ObserveDelivery(delivery))return;
            // Frozen evidence survives the caller's one-use delivery disposal.
            if(original.DeliveryRetention!=null||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(
                checked(4L*System.Text.Encoding.UTF8.GetByteCount(evidence.ToString(SaveOptions.DisableFormatting))),out original.DeliveryRetention))return;
            original.Delivery=new XElement(evidence);
            if(!WriteExact(original.Path+".Delivered",System.Text.Encoding.UTF8.GetBytes(evidence.ToString(SaveOptions.DisableFormatting)))||
                !WriteExact(original.Path+".OwnerAfter",original.OwnerFile.After))return;
            original.Delivered=true;
            GameManager.Instance.SaveLocalPlayerData();
            if(original.Binding.World.IsRemote())GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
        }
        catch{/* Physical native output happened: original stays attempted/unknown. */}
    }
}
