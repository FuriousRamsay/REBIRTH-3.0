// Actual Third DTO capture adapter. Does not authenticate command or recreate live receipt after reload.
public static class SeedRecoveryOriginalCommandAdapter
{
    public static SeedRecoveryRecord Capture(System.Guid worldId,System.Guid characterId,
        SeedPlacementOriginalCommandReview original,SeedDebitReceipt liveReceipt)
    {
        if(original==null||liveReceipt==null)throw new System.ArgumentNullException();
        return new SeedRecoveryRecord {WorldId=worldId,CharacterId=characterId,Epoch=original.Epoch,Nonce=original.Nonce,
            Actor=original.Actor,Slot=original.Slot,OriginalCount=original.OriginalCount,Flags=original.Flags,Density=original.Density,Texture=original.Texture,
            X=original.Position.x,Y=original.Position.y,Z=original.Position.z,TargetRaw=original.Target.rawData,TargetDamage=original.Target.damage,
            OldRaw=original.ExpectedOld.rawData,OldDamage=original.ExpectedOld.damage,Seed=original.CopySeedIdentity(),Debit=liveReceipt.State};
    }
}
