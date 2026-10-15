// Uses the installed authoritative character service rather than accepting a
// character id supplied by a window, module item or remote request.
internal static class RebirthCamperCharacterAccess
{
    internal static string CurrentCreation(EntityPlayer player)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())
        {
            var world=GameManager.Instance?.World;
            if(world?.IsRemote()==true)
                return player is EntityPlayerLocal local?RebirthCamperCharacterScope.ResolveLocal(local):null;
            if(!ThreadManager.IsMainThread()||player==null||player.IsDead()||world==null||world.IsRemote()||
                !System.Object.ReferenceEquals(player.world,world)||!System.Object.ReferenceEquals(world.GetEntity(player.entityId),player))return null;
            var identity=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)?.PrimaryId;
            if(identity==null&&System.Object.ReferenceEquals(world.GetPrimaryPlayer(),player))
                identity=GameManager.Instance.getPersistentPlayerID(null);
            return identity==null||string.IsNullOrWhiteSpace(identity.CombinedString)?null:"native-mode:"+identity.CombinedString;
        }
        if(!ThreadManager.IsMainThread()||player==null||player.IsDead()||
            RebirthCharacterCreationHoldService.IsHeld(player)||
            !RebirthWorldCharacterService.TryGet(player,out var owner)||owner==null||!owner.IsComplete||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner))return null;
        return owner.Origin?.CreationId;
    }
}
