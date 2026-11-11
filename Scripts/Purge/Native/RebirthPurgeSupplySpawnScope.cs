using System;

// Only the original native controller call carries this scope. Capture its
// exact first crate before native registration/callbacks/broadcast; subsequent
// unrelated/nested entities cannot inherit its original saved entitlement.
internal sealed class RebirthPurgeSupplySpawnScope:IDisposable
{
    [ThreadStatic] private static RebirthPurgeSupplySpawnScope active;
    private readonly RebirthPurgeSupplySpawnScope previous;
    private readonly World world;
    private readonly RebirthPurgeSupplyCoordinator.Reservation request;
    private EntitySupplyCrate target;
    private bool attached;

    internal RebirthPurgeSupplySpawnScope(World world,RebirthPurgeSupplyCoordinator.Reservation request)
    {
        this.world=world;
        this.request=request;
        previous=active;active=this;
    }
    internal static void BeforeSpawn(World __instance,Entity _entity)
    {
        var scope=active;var crate=_entity as EntitySupplyCrate;
        if(scope==null||scope.target!=null||crate==null||!ReferenceEquals(__instance,scope.world))return;
        scope.target=crate;
        scope.attached=RebirthPurgeSupplyCrateSerialization.TryAttachBeforeSpawn(crate,scope.request);
        if(!scope.attached)throw new InvalidOperationException("Original supply custody could not precede native crate registration.");
        scope.request.ProjectOwner(crate);
    }
    internal void ObserveRegistration()
    {
        if(attached&&target!=null&&world.Entities.dict.TryGetValue(target.entityId,out var actual)&&ReferenceEquals(actual,target))
            request.Spawned=true;
    }
    public void Dispose(){if(ReferenceEquals(active,this))active=previous;}
}