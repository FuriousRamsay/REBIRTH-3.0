using System;
using System.Reflection;

// PREPARED, NOT RUN. Harness performs a REAL native world transition between
// construction and Verify. No call to domain reset is used to fake the event.
internal sealed class mpA_logistics_world_cleanup_fixture
{
    private readonly World previous;private readonly EntityPlayerLocal player;private readonly Guid operation;private readonly int phase;private readonly float held;private readonly object priorResult,priorParts;
    internal mpA_logistics_world_cleanup_fixture(EntityPlayerLocal actual)
    {
        player=actual??throw new ArgumentNullException(nameof(actual));previous=actual.world;
        if(!ReferenceEquals(GameManager.Instance?.World,previous)||!RebirthLogisticsOwnerReceipt.TryOperation(actual.Buffs,out operation,out phase))throw new Exception("Requires actual native owner original before world transition.");
        held=actual.Buffs.GetCustomVar("rbLogisticsOwnerHeld");
        priorResult=typeof(RebirthLogisticsOriginalTransport).GetField("latest",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        priorParts=typeof(RebirthLogisticsOriginalTransport).GetField("parts",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    }
    internal void AfterActualWorldUpdate()
    {
        var current=GameManager.Instance?.World;if(ReferenceEquals(current,previous))throw new Exception("Harness has not performed real world transition.");
        object Read(Type type,string field)=>type.GetField(field,BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        if(ReferenceEquals(Read(typeof(RebirthLogisticsOriginalTransport),"clientWorld"),previous)||ReferenceEquals(Read(typeof(RebirthLogisticsOwnerReceipt),"heldWorld"),previous)||
            ReferenceEquals(Read(typeof(RebirthLogisticsOwnerReceipt),"heldPlayer"),player))throw new Exception("Old original world/player references retained after actual update.");
        if(priorResult!=null&&ReferenceEquals(Read(typeof(RebirthLogisticsOriginalTransport),"latest"),priorResult)||priorParts!=null&&ReferenceEquals(Read(typeof(RebirthLogisticsOriginalTransport),"parts"),priorParts))throw new Exception("Old result/material graph retained after world change.");
        if(!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var after,out int afterPhase)||after!=operation||afterPhase!=phase||player.Buffs.GetCustomVar("rbLogisticsOwnerHeld")!=held)
            throw new Exception("Volatile cleanup changed original native receipt.");
    }
}
