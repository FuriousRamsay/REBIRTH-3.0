using System;
static class ActualSettlementReadback{
static EntityPlayerLocal owner;static World boundWorld;static object session;static string creation;static RebirthBackpackLibrarySettlement lastSettlement;
internal static void Seed(World world,EntityPlayerLocal player,object peer,string character,RebirthBackpackLibrarySettlement outcome){boundWorld=world;owner=player;session=peer;creation=character;lastSettlement=outcome;}
static bool Resolve(World world,int id,out EntityPlayerLocal player,out object peer,out string current){player=NativeScope.Player;peer=NativeScope.Peer;current=NativeScope.Creation;return NativeScope.Allowed&&ReferenceEquals(world,NativeScope.World)&&player!=null&&id==player.Id;}    public static bool TryGetSettlement(World world,int playerId,Guid transaction,out RebirthBackpackLibrarySettlement result)
    {
        result=null;
        if(transaction==Guid.Empty||lastSettlement==null||
            !Resolve(world,playerId,out var player,out var connection,out var current)||
            !ReferenceEquals(boundWorld,world)||!ReferenceEquals(owner,player)||!ReferenceEquals(session,connection)||creation!=current||
            lastSettlement.CreationId!=current||lastSettlement.TransactionId!=transaction.ToString("N"))return false;
        result=lastSettlement;return true;
    }
    // Advisory refresh floor only; never an inventory application authority.
    internal static bool TryGetSettledRevision(World world,int playerId,out long revision)
    {
        revision=-1;var retained=lastSettlement;
        if(retained==null||!Guid.TryParseExact(retained.TransactionId,"N",out var transaction)||
            !TryGetSettlement(world,playerId,transaction,out var current)||!ReferenceEquals(retained,current))return false;
        revision=current.GearRevision;return revision>=0;
    }
}