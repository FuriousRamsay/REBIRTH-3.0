using System;
internal static class ModelProgram
{
    private static int checks;
    private static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
    private static EModelRebirthSdcsNpc Make(out EntityAlive entity)
    {
        entity=new EntityAlive();var model=new EModelRebirthSdcsNpc();
        model.Init(new World(),entity,new EModelInstanceAssets());return model;
    }
    private static RebirthHumanNpcAppearanceDescriptor Appearance(int seed=1)=>new()
    {IsValid=true,Pipeline=RebirthHumanNpcModelPipeline.SDCS,Seed=seed,Archetype="BaseMale"};
    private static void Main()
    {
        var model=Make(out var entity);
        Check(entity.equipment.Subscribers==1,"entity-scoped subscription");
        Check(model.avatarController!=null,"logical controller exists before client mesh generation");
        Check(model.GetHeadTransform()!=null&&model.GetPelvisTransform()==null&&model.NeckTransform==null,"pre-mesh native transform queries tolerate no catalogue");
        Check(GameUtils.FindTagInChilds(entity.RootTransform,"LargeEntityBlocker")!=null,"blocker created during Init");
        model.ApplyAppearance(Appearance());
        Check(model.RebirthMeshBuildCount==1&&model.visible,"one visible initial model");
        Check(model.CommonInitializations==1,"native common initialization runs after deferred model creation");
        var rig=model.baseRig;
        model.Update();Check(model.RebirthMeshBuildCount==1,"native callback during initial generation does not rebuild");
        for(int i=0;i<20;i++)model.RefreshAppearanceMeshes();
        model.Update();Check(model.RebirthMeshBuildCount==1,"unchanged snapshots avoid meshes entirely");
        entity.equipment.Items[0]=new ItemValue{ItemClass=new ItemClass()};
        for(int i=0;i<20;i++)entity.equipment.Fire();
        Check(model.RebirthMeshBuildCount==1,"slot writes defer work");
        model.Update();Check(model.RebirthMeshBuildCount==2&&ReferenceEquals(model.baseRig,rig),"batch produces one in-place build");
        model.Update();Check(model.RebirthMeshBuildCount==2,"recursive native cosmetic callback suppressed");
        entity.equipment.Cosmetics[0]=new ItemClass();entity.equipment.Fire();model.Update();
        Check(model.RebirthMeshBuildCount==3,"cosmetic change rebuilds");
        entity.equipment.Items[0]=null;entity.equipment.Fire();model.Update();
        Check(model.RebirthMeshBuildCount==4,"armor removal rebuilds");
        model.ApplyAppearance(Appearance());Check(model.RebirthMeshBuildCount==4,"identical appearance is a no-op");
        model.SetVisible(false,true);entity.equipment.Fire();model.Update();
        Check(model.RebirthMeshBuildCount==4&&!model.visible,"hidden unchanged model stays hidden");
        entity.equipment.Cosmetics[0]=null;entity.equipment.Fire();model.Update();
        Check(!SDCSUtils.LastVisible&&!model.visible,"hidden changed model does not become visible");
        entity.PhysicsTransform.gameObject.layer=14;model.PostInit();Check(entity.PhysicsTransform.gameObject.layer==15,"alive NPC collision layer restored");
        entity.PhysicsTransform.gameObject.layer=19;model.PostInit();Check(entity.PhysicsTransform.gameObject.layer==19,"explicit other physics layer preserved");
        entity.Dead=true;entity.PhysicsTransform.gameObject.layer=14;model.PostInit();Check(entity.PhysicsTransform.gameObject.layer==14,"dead physics untouched");
        entity.equipment.Items[0]=new ItemValue{ItemClass=new ItemClass()};entity.equipment.Fire();
        SDCSUtils.Throw=true;try{model.Update();throw new Exception("expected simulated failure");}catch(InvalidOperationException){}finally{SDCSUtils.Throw=false;}
        model.RefreshAppearanceMeshes();model.Update();Check(model.RebirthMeshBuildCount==6,"build guard released on exception");
        model.OnDestroy();Check(entity.equipment.Subscribers==0&&model.BaseDestroyed,"unsubscribe and inherited cleanup");
        int before=model.RebirthMeshBuildCount;entity.equipment.Fire();model.Update();Check(model.RebirthMeshBuildCount==before,"destroyed subscriber receives no new events");

        var retry=Make(out var retryEntity);SDCSUtils.Fail=true;retry.ApplyAppearance(Appearance());SDCSUtils.Fail=false;
        retry.RefreshAppearanceMeshes();retry.Update();Check(retry.RebirthAppearanceApplied&&retry.RebirthMeshBuildCount==1,"successful retry does not build twice");
        var automatic=Make(out _);SDCSUtils.Fail=true;automatic.ApplyAppearance(Appearance());SDCSUtils.Fail=false;
        automatic.Update();Check(automatic.RebirthAppearanceApplied&&automatic.RebirthMeshBuildCount==1,"initial failure retries without an unrelated equipment event");
        var exhausted=Make(out _);SDCSUtils.Fail=true;int attempts=SDCSUtils.Calls;exhausted.ApplyAppearance(Appearance());
        for(int i=0;i<10;i++)exhausted.Update();SDCSUtils.Fail=false;
        Check(SDCSUtils.Calls-attempts==3&&!exhausted.RebirthAppearanceApplied,"automatic retry stops after three attempts");
        GameManager.IsDedicatedServer=true;int calls=SDCSUtils.Calls;
        var dedicated=Make(out var serverEntity);dedicated.ApplyAppearance(Appearance());
        Check(dedicated.avatarController is AvatarControllerDummy,"dedicated gets native logical controller without a mesh");
        Check(dedicated.GetHeadTransform()!=null&&dedicated.GetPelvisTransform()==null&&dedicated.NeckTransform==null,"dedicated native transform queries need no visual catalogue");
        serverEntity.equipment.Fire();dedicated.RefreshAppearanceMeshes();dedicated.Update();
        Check(SDCSUtils.Calls==calls&&dedicated.RebirthAppearanceApplied&&!dedicated.visible,"dedicated retains descriptor without visual calls");
        GameManager.IsDedicatedServer=false;
        Console.WriteLine("PASS: "+checks+" actual SDCS model checks with simulated native/Unity boundary.");
    }
}
