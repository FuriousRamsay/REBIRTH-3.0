using System;
using System.Xml;

public static partial class RebirthDogStorageService
{
    private static World storageWorld;
    private static bool CurrentNativeStorageDog(EntityRebirthDogCompanion dog)
    {
        var world=GameManager.Instance?.World;
        return ThreadManager.IsMainThread()&&world!=null&&dog!=null&&dog.NativeIdentityReadable&&ReferenceEquals(dog.world,world)&&ReferenceEquals(world.GetEntity(dog.entityId),dog);
    }
    internal static bool CurrentServerStorageDog(EntityRebirthDogCompanion dog,bool bind=false)
    {
        if(!CurrentNativeStorageDog(dog)||dog.world.IsRemote()||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        lock(Sync){if(storageWorld!=null&&!ReferenceEquals(storageWorld,dog.world))return false;if(storageWorld==null){if(OpenServerEntities.Count!=0)return false;if(bind)storageWorld=dog.world;}return true;}
    }
    private static bool CurrentLocalStorageDog(EntityRebirthDogCompanion dog)
    {return CurrentNativeStorageDog(dog)&&(dog.world.IsRemote()||CurrentServerStorageDog(dog));}
    private static void ReleaseCurrentLocalStorageLock(EntityRebirthDogCompanion dog)
    {if(CurrentLocalStorageDog(dog))LockManager.Instance?.UnlockRequestLocal();}
    internal static bool CanRetireEmptyStorage
    {get{lock(Sync)return ThreadManager.IsMainThread()&&OpenServerEntities.Count==0;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareEmptyStorageQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||!CanRetireEmptyStorage||(storageWorld!=null&&!ReferenceEquals(storageWorld,scope.World)))throw new InvalidOperationException("Active native edit lease/UI/native LockManager requires its own original custody proof");
            var world=storageWorld;var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("dogStorage");doc.AppendChild(root);root.SetAttribute("emptyOnly","1");
            return new RebirthNpcSaveQuarantineDomain("dog-storage-empty",doc,
                ()=>{lock(Sync)return CanRetireEmptyStorage&&ReferenceEquals(storageWorld,world);},
                ()=>{lock(Sync){if(!CanRetireEmptyStorage)throw new InvalidOperationException("Native dog edit lease changed after preflight");storageWorld=null;}},
                ()=>{lock(Sync)storageWorld=world;});
        }
    }
}
