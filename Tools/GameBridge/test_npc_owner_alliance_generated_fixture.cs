using System;
public enum RebirthNpcOwnershipKind { None, Player, Settlement }
public sealed class RebirthNpcRuntimeState { public int StableId; public string OwnerId; public RebirthNpcOwnershipKind OwnershipKind; }
public static class NpcOwnerPolicyFixture {
    public static bool AreAllied(RebirthNpcRuntimeState left,RebirthNpcRuntimeState right)
    {
        if(left==null||right==null)return false;
        if(left.StableId==right.StableId)return true;
        if(left.OwnershipKind!=RebirthNpcOwnershipKind.None&&left.OwnershipKind==right.OwnershipKind&&!string.IsNullOrWhiteSpace(left.OwnerId)&&!string.IsNullOrWhiteSpace(right.OwnerId)&&string.Equals(left.OwnerId,right.OwnerId,StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }
public static string Run(){
var a=new RebirthNpcRuntimeState{StableId=1,OwnershipKind=RebirthNpcOwnershipKind.Player,OwnerId=""};
var b=new RebirthNpcRuntimeState{StableId=2,OwnershipKind=RebirthNpcOwnershipKind.Player,OwnerId=""};
if(AreAllied(a,b))throw new Exception("Empty owner IDs create alliance");
a.OwnerId=" ";b.OwnerId=" ";if(AreAllied(a,b))throw new Exception("Whitespace owner IDs create alliance");
a.OwnerId="OWNER";b.OwnerId="owner";if(!AreAllied(a,b))throw new Exception("Valid shared owner lost alliance");
b.OwnershipKind=RebirthNpcOwnershipKind.Settlement;if(AreAllied(a,b))throw new Exception("Different custody kind creates alliance");
b.OwnerId="other";b.OwnershipKind=RebirthNpcOwnershipKind.Player;if(AreAllied(a,b))throw new Exception("Different owners create alliance");
b.StableId=1;if(!AreAllied(a,b))throw new Exception("Self alliance lost");
if(AreAllied(null,b))throw new Exception("Null actor creates alliance");
return "PASS: actual AreAllied method, seven identity/ownership boundaries.";
}
}