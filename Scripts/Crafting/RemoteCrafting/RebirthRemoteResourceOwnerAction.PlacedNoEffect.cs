using System;

internal static partial class RebirthRemoteResourceOwnerAction
{
    internal static bool CanReleaseEndedPlacedNoEffect(RebirthRemoteResourcePlacedStationOriginal.EndedNeverEnteredClient proof)
    {
        if(proof?.Player==null||proof.Original==null)return false;var original=proof.Original;var player=proof.Player;
        float phase=player.Buffs.GetCustomVar(Receipt(original.Operation));if(phase!=0f&&phase!=10f)return false;
        return !holds.TryGetValue(original.Operation,out var hold)||hold.Original.Digest==original.Digest&&!hold.Refund&&!hold.BlockRefund&&!hold.BlockingOnly&&
            hold.SourceOperation==Guid.Empty&&string.IsNullOrEmpty(hold.SourceBinding)&&hold.Before.Matches(hold.After);
    }
    internal static bool ReleaseEndedPlacedNoEffect(RebirthRemoteResourcePlacedStationOriginal.EndedNeverEnteredClient proof)
    {
        if(proof?.Current!=true)return false;
        holds.Remove(proof.Original.Operation);
        if(proof.Player.Buffs.GetCustomVar(Receipt(proof.Original.Operation))==10f)
        {proof.Player.Buffs.SetCustomVar(Receipt(proof.Original.Operation),0f,true);proof.Player.Buffs.RemoveCustomVar(Receipt(proof.Original.Operation));}
        GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
    }
}
