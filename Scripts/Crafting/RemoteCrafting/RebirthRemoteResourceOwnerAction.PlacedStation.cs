using System;

internal static partial class RebirthRemoteResourceOwnerAction
{
    internal static bool ReleasePlacedTerminal(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof)
    {
        if(!RebirthRemoteResourceClientOriginal.Current(player,original)||!RebirthRemoteResourcePlacedStationOriginal.HasClientRetired(original,source,binding,proof))return false;
        if(holds.TryGetValue(original.Operation,out var hold))
        {
            if(hold.Original.Digest!=original.Digest||hold.Refund||!ReferenceEquals(hold.Player,player)&&Current(hold))return false;
            if(hold.SourceOperation==Guid.Empty)
            {if(!hold.Before.Matches(hold.After)||!hold.Before.Matches(RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value)))return false;}
            else if(hold.SourceOperation!=source||hold.SourceBinding!=binding)return false;
            holds.Remove(original.Operation);
        }
        player.Buffs.SetCustomVar(Receipt(original.Operation),0f,true);player.Buffs.RemoveCustomVar(Receipt(original.Operation));
        return true;
    }
}
