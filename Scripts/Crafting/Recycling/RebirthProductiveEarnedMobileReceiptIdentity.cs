using System;
using System.Linq;

internal static partial class RebirthProductiveEarnedCoordinator
{
    private static bool MatchesMobileRemoteToolReceipt(MobileRemoteTools source,RebirthCamperTransferJournal.Record receipt,Guid vehicle)
    {
        try
        {
            return source!=null&&receipt!=null&&source.Operation!=Guid.Empty&&!string.IsNullOrEmpty(source.Account)&&!string.IsNullOrEmpty(source.Creation)&&
                vehicle!=Guid.Empty&&receipt.Vehicle==vehicle&&receipt.Operation==source.Operation&&
                receipt.OwnerCreation=="remote:"+source.Account+":"+source.Creation&&
                RebirthProductiveEarnedMobileOrigin.Digest(receipt.Before).SequenceEqual(source.TransferBefore)&&
                RebirthProductiveEarnedMobileOrigin.Digest(receipt.After).SequenceEqual(source.TransferAfter);
        }
        catch{return false;}
    }
}
