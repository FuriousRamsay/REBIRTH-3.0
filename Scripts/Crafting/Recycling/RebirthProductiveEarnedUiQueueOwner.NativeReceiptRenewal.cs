using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    private static readonly object nativeReceiptRenewalIssuer=new object();
    internal static bool ImportActualNativeOriginalReceipts(RebirthProductiveEarnedUiDeferredTransport.ClientHydration actual)
    {
        var section=actual?.Header?.Element("nativeOriginalReceipts");if(section==null)return true;
        if(!RebirthProductiveEarnedUiDeferredTransport.IsActualHydrationRead(actual)||(int?)section.Attribute("version")!=1||(bool?)section.Attribute("observationOnly")!=true)return false;
        var entries=section.Elements("original").ToArray();if(entries.Length<1||entries.Length>4)return false;
        foreach(var entry in entries)
        {
            if(!Guid.TryParseExact((string)entry.Attribute("operation"),"N",out var operation)||
                !RebirthWorkstationCompletionPipeline.TryFindRemoteNativeReceipt(operation,actual.World,actual.Station,out var token)||
                token.NativeOriginal==null||token.NativeOriginal.RemoteNativeNonce.ToString("N")!=(string)entry.Attribute("nonce")||
                token.NativeOriginal.RemoteNativeSourceHash!=(string)entry.Attribute("source")||token.Source.Birth.ToString("N")!=(string)entry.Attribute("birth")||
                token.AllContributions.Any(member=>member.Imported!=null||member.HostLocal!=null||!member.CurrentSourceWithoutRow()||
                    !RebirthProductiveEarnedUiOwnerFile.CanRenewNativeReceiptReference(member)))return false;
            XElement ledger=null;XElement[] members=null;IDisposable retention=null;
            try
            {
                if(!TryCopyNativeOriginalLedger(token,out ledger,out retention)||
                    RebirthWorkstationCompletionOriginal.Hash(ledger.ToString(SaveOptions.DisableFormatting))!=(string)entry.Attribute("ledger")||
                    RebirthWorkstationCompletionOriginal.Hash((string)ledger.Element("aggregate"))!=(string)entry.Attribute("aggregate"))return false;
                members=ledger.Element("members").Elements("member").ToArray();var ids=entry.Elements("member").ToArray();
                if(ids.Length!=members.Length)return false;
                for(int i=0;i<members.Length;i++)
                    if((string)ids[i].Attribute("operation")!=(string)members[i].Attribute("operation")||(string)ids[i].Attribute("kind")!=(string)members[i].Attribute("kind")||
                        (string)ids[i].Attribute("source")!=(string)members[i].Attribute("source")||
                        (string)ids[i].Attribute("original")!=RebirthWorkstationCompletionOriginal.Hash(members[i].ToString(SaveOptions.DisableFormatting)))return false;
                CraftCompleteData[] rows;
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null)return false;
                    rows=actual.Station.CraftCompleteList?.Where(row=>RowImage(row).SequenceEqual(token.aggregate.Image)).ToArray();
                }
                if(rows?.Length!=1||rows[0].CrafterEntityID!=token.RecipientEntityId)return false;
                // Same typed original event and full retained ledger, observed
                // through this exact authenticated native reader. Only its
                // current native reference changes; no new Add/Check/XP or
                // saved/Ready/terminal authority is issued.
                token.aggregate.Receipt=rows[0];
                foreach(var member in token.AllContributions)member.RenewCurrentReference(nativeReceiptRenewalIssuer,rows[0]);
            }
            finally{members=null;ledger=null;retention?.Dispose();}
        }
        return true;
    }
}

internal static partial class RebirthProductiveEarnedUiOwnerFile
{
    internal static bool CanRenewNativeReceiptReference(RebirthProductiveEarnedUiQueueOwner.DeferredReceipt receipt)
    {
        if(!ThreadManager.IsMainThread()||receipt==null)return false;
        lock(gate)return !deferred.Any(value=>ReferenceEquals(value.Receipt,receipt)&&(value.Consumed||value.Completed||value.OwnerTerminalSaved))&&
            !(consuming?.Members?.Any(value=>ReferenceEquals(value.Receipt,receipt))==true)&&
            !localOwners.Values.Any(value=>!value.Closed&&value.Members.Any(member=>ReferenceEquals(member.Receipt,receipt)));
    }
}
