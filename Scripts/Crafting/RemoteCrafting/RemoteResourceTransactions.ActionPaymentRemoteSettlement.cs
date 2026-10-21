using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    private static bool SaveRemoteActionOwnerBefore(ActionPayment payment)
    {
        var original=payment.RemoteOwner;if(!RemoteActionOriginalCurrent(original)||!ReferenceEquals(original.Actor,payment.Actor)||original.Operation!=payment.Operation)return false;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var scratch))return false;
        using(scratch)
        {
            byte[] confirmed=EncodeActionRemoteEnvelope(original.Click,original.NativeBefore);
            if(!ActionEnvelopeFileEquals(Path.Combine(original.Directory,"confirmed.native"),confirmed))return false;
            string path=Path.Combine(payment.Directory,"owner-before.native");ActionPath(path);
            if(File.Exists(path)){if(!ActionBytes(path,original.NativeBefore.Length).SequenceEqual(original.NativeBefore))return false;}
            else{using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(original.NativeBefore,0,original.NativeBefore.Length);output.Flush(true);}if(!ActionBytes(path,original.NativeBefore.Length).SequenceEqual(original.NativeBefore))return false;}
            ActionRecord(payment,"owner-before-saved",new XElement("native",new XAttribute("length",original.NativeBefore.Length),new XAttribute("nativeId",payment.NativeEntityId),new XAttribute("original",original.Operation.ToString("N"))));return true;
        }
    }
    private static bool AdvanceRemoteActionOwner(ActionPayment payment,bool returned)
    {
        if(payment?.ActorCurrent()!=true||!RemoteActionOriginalCurrent(payment.RemoteOwner))return false;
        var legs=payment.Legs.Where(leg=>leg.RemoteOwner).ToArray();if(legs.Length==0)return false;
        if(returned)
        {
            if(payment.State!=ActionPaymentState.Returning||payment.PublicationEntered||!payment.OwnerAfterSaved||legs.Any(leg=>leg.State!=ActionLegState.Debited&&leg.State!=ActionLegState.ReturnAttempted&&leg.State!=ActionLegState.Returned))return false;
            if(!payment.RemoteRefundEntered)
            {
                ActionRecord(payment,"owner-return-intent",new XElement("originalOwner",new XElement("bag",ActionFrame(payment.OwnerBagBefore)),new XElement("belt",ActionFrame(payment.OwnerBeltBefore))));
                payment.RemoteRefundEntered=true;foreach(var leg in legs)leg.State=ActionLegState.ReturnAttempted;
            }
            QueueActionRemoteGrant(payment,1);
            if(!SaveActionOwner(payment,"return"))return false;
            ActionRecord(payment,"owner-returned",new XElement("native",new XAttribute("source","owner-return.native")));
            foreach(var leg in legs){leg.State=ActionLegState.Returned;leg.Progress=leg.Before;for(int i=0;i<leg.ReturnedSlots.Length;i++)leg.ReturnedSlots[i]=true;leg.ReturnSaved=true;}
            payment.OwnerReturnSaved=true;return true;
        }
        if(payment.State!=ActionPaymentState.Paying||!payment.OwnerBeforeSaved||!payment.TargetBeforeSaved||payment.Legs.Any(leg=>!leg.BeforeSaved))return false;
        if(!payment.RemoteDebitEntered)
        {
            ActionRecord(payment,"owner-debit-intent",new XElement("fullNativeBefore",new XAttribute("source","owner-before.native")));
            payment.RemoteDebitEntered=true;foreach(var leg in legs)leg.State=ActionLegState.DebitAttempted;
        }
        QueueActionRemoteGrant(payment,0);
        if(SaveActionOwner(payment,"after"))
        {
            ActionRecord(payment,"owner-debited",new XElement("native",new XAttribute("source","owner-after.native")));
            foreach(var leg in legs){leg.State=ActionLegState.Debited;leg.Progress=leg.After;for(int i=0;i<leg.DebitedSlots.Length;i++)leg.DebitedSlots[i]=true;leg.AfterSaved=true;}
            payment.OwnerAfterSaved=true;return true;
        }
        if(SaveActionOwner(payment,"refused"))
        {
            foreach(var leg in legs)
            {
                leg.State=ActionLegState.Refused;leg.RefusalStage="owner-native-refused-source-"+payment.Legs.IndexOf(leg);
                leg.RefusalDetail=new XElement("nativeOwnerOriginalNotRun",new XAttribute("native","owner-refused.native"),new XElement("fullBefore",ActionFrame(leg.Before)));
                RecordActionRefusal(payment,leg);
            }
            ActionRecord(payment,"returning");payment.State=ActionPaymentState.Returning;
        }
        return false;
    }
}
