using System;

public abstract partial class EntityRebirthNPC
{
    private bool rebirthNativeRuntimeAdoptionOpen;
    internal bool NativeAdoptionOriginalAi=>rebirthDomainRegistrationPending?rebirthDomainOriginalAi:hasAI;
    internal bool CanStageNativeRuntime(RebirthNpcRuntimeNativeAdoption.NativeReentry source,RebirthNpcRuntimeState prior)
        =>rebirthNativeRuntimeAdoptionOpen&&!rebirthPreparedRestorationPending&&source?.Current()==true&&
            ReferenceEquals(source.Actor,this)&&RebirthNpcRuntimeNativeAdoption.IsIncomingHeld(this)&&
            (ReferenceEquals(rebirthRuntimeState,prior)||rebirthDomainRegistrationPending&&prior==null);
    internal bool InstallStagedNativeRuntime(RebirthNpcRuntimeNativeAdoption.NativeReentry source,RebirthNpcRuntimeState saved)
    {
        if(!rebirthNativeRuntimeAdoptionOpen||source?.Current()!=true||!ReferenceEquals(source.Actor,this)||saved==null||
            saved.StableId!=source.StableId||saved.ProfileId!=RebirthProfileId||!RebirthNpcRuntimeNativeAdoption.IsIncomingHeld(this)||
            !RebirthNpcRuntimeRegistry.TryGet(entityId,out var registered)||!ReferenceEquals(registered,saved))return false;
        rebirthRuntimeState=saved;rebirthDomainRegistrationPending=false;hasAI=false;return true;
    }
}
