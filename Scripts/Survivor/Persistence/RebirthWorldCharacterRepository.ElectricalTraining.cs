using System;

public static partial class RebirthWorldCharacterRepository
{
    // Positive readback of the final owning character image. A dirty cache or
    // a backup is not proof that the original training receipt was published.
    internal static bool HasSavedElectricalActionTraining(RebirthStablePlayerIdentity identity,string creation,Guid operation)
    {
        if(!ThreadManager.IsMainThread()||!serverAuthority||identity==null||string.IsNullOrEmpty(creation)||operation==Guid.Empty)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                return serverAuthority&&TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)&&!migrated&&
                    saved?.Progression!=null&&RebirthSurvivorRequestScope.Matches(creation,saved.Origin?.CreationId)&&
                    saved.Progression.SkillAwardReceipts.Contains("electrical-action:"+operation.ToString("N"))&&
                    serverAuthority&&path==GetPath(identity.StorageKey);
            }
            catch{return false;}
        }
    }
}
