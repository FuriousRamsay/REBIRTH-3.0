using System;

// Only the original native controller call carries this scope. Capture its
// exact first crate before native registration/callbacks/broadcast; subsequent
// unrelated/nested entities cannot inherit its original saved entitlement.
internal sealed class RebirthPurgeSupplySpawnScope:IDisposable
{
    [ThreadStatic] private static RebirthPurgeSupplySpawnScope active;
    private readonly RebirthPurgeSupplySpawnScope previous;
    private readonly World world;
    private readonly RebirthPurgeSupplyCrateStamp stamp;
    private readonly object owner;
    private readonly Func<bool> current;
    internal EntitySupplyCrate Target{get;private set;}
    internal bool Attached{get;private set;}
    internal RebirthPurgeSupplySpawnScope(World world,RebirthPurgeSupplyCrateStamp stamp,object owner,Func<bool> current)
    {
        this.world=world??throw new ArgumentNullException(nameof(world));this.stamp=stamp??throw new ArgumentNullException(nameof(stamp));
        this.owner=owner??throw new ArgumentNullException(nameof(owner));this.current=current??throw new ArgumentNullException(nameof(current));
        previous=active;active=this;
    }
    internal static void BeforeSpawn(World __instance,Entity _entity)
    {
        var scope=active;var crate=_entity as EntitySupplyCrate;
        if(scope==null||scope.Target!=null||crate==null||!ReferenceEquals(__instance,scope.world)||!scope.current())return;
        scope.Target=crate;
        scope.Attached=RebirthPurgeSupplyCrateSerialization.TryAttachBeforeSpawn(crate,scope.stamp,scope.owner,scope.current);
        if(scope.owner is RebirthPurgeSupplyCoordinator.Reservation reservation)reservation.ProjectOwner(crate);
        if(!scope.Attached)throw new InvalidOperationException("Original supply custody could not precede native crate registration.");
    }
    public void Dispose(){if(ReferenceEquals(active,this))active=previous;}
}