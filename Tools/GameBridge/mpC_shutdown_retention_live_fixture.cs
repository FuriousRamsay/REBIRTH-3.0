// Source-only live fixture. Compile with the DEBUG audit fixture host; NOT_RUN.
// Requires a disposable server save with an active NPC and a real authenticated player.
// Every mutation/save/shutdown below calls production code; no SaveAll result is doubled.
using System;
using System.IO;
using System.Reflection;

public static class MpCShutdownRetentionScenario
{
    static void Need(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static void RequireDisposableSave(){Need(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest","disposable CodexTest save required" );Need(ThreadManager.IsMainThread(),"native main thread required" );}
    static FieldInfo Fault=>typeof(RebirthNpcPersistenceCoordinator).GetField("BeforeCommitScenario",BindingFlags.Static|BindingFlags.NonPublic);
    static readonly Type[] Callbacks={typeof(RebirthNpcInventoryPersistenceModApi),typeof(RebirthNpcEquipmentPersistenceModApi),typeof(RebirthNpcExecutionPersistenceModApi),typeof(RebirthNpcSettlementPersistenceModApi)};
    static void Callback(Type type,bool game)
    {
        var method=type.GetMethod(game?"OnGameShutdown":"OnWorldShuttingDown",BindingFlags.Static|BindingFlags.NonPublic);
        Need(method!=null,"actual independent callback missing");
        var element=method.GetParameters()[0].ParameterType.GetElementType();
        method.Invoke(null,new[]{Activator.CreateInstance(element)});
    }
    // Invoke separately on fresh actual seeded saves for each domain and both orders.
    public static void NpcFailureThenRetry(string domain,bool reverse)
    {
        RequireDisposableSave();
        Need(ThreadManager.IsMainThread(),"native main thread required");
        var world=GameManager.Instance.World;var save=Path.GetFullPath(GameIO.GetSaveGameDir());
        int active=RebirthNpcLifecycle.GetSnapshot().ActiveEntities;Need(active>0,"seed a real active NPC");
        Need(Fault!=null,"DEBUG actual CommitStore boundary not installed");
        int attempts=0;
        Fault.SetValue(null,(Action<string>)(name=>{if(name==domain){attempts++;throw new IOException("MP046 injected actual domain publication failure");}}));
        try
        {
            for(int i=0;i<Callbacks.Length;i++)Callback(Callbacks[reverse?Callbacks.Length-1-i:i],i%2!=0);
            Need(attempts>=2,"both actual bounded save attempts must reach selected domain");
            Need(!RebirthNpcPersistenceCoordinator.LastSaveSucceeded,"failed generation cannot be successful");
            Need(RebirthNpcLifecycle.GetSnapshot().ActiveEntities==active,"failure cleared original active runtime");
            Need(!RebirthNpcPersistenceCoordinator.TryResetAfterSaveFor(world,save),"failed original reset was admitted");
        }
        finally{Fault.SetValue(null,null);}
        RebirthNpcLifecycle.RequestCoordinatedShutdown(false);
        Need(RebirthNpcPersistenceCoordinator.LastSaveSucceeded,"same-original real retry did not commit");
        Need(RebirthNpcLifecycle.GetSnapshot().ActiveEntities==0,"positive coordinated reset did not retire runtime");
        RebirthNpcLifecycle.RequestCoordinatedShutdown(true);
        Need(RebirthNpcLifecycle.GetSnapshot().ActiveEntities==0,"repeat shutdown changed terminal state");
    }
    // Call after an actual failed shutdown and native world transition, with the retained
    // original World/save captured before that transition. No fake World is constructed.
    public static void VerifyActualReplacementRefusesOriginalRetirement(World original,string originalSave)
    {
        RequireDisposableSave();
        Need(!ReferenceEquals(original,GameManager.Instance.World),"actual replacement World required");
        Need(!RebirthNpcPersistenceCoordinator.TryResetAfterSaveFor(original,originalSave),"replacement World retired original NPC custody");
        Need(!RebirthWorldCharacterRepository.TryFlush("replacement-must-refuse"),"replacement World saved original survivor cache");
        Need(!RebirthMetabolismStateRepository.TryFlush("replacement-must-refuse"),"replacement World saved original metabolism cache");
        Need(!RebirthWorldCharacterRepository.TryResetAfterFlush(false),"replacement World cleared original survivor cache");
        Need(!RebirthMetabolismStateRepository.TryResetAfterFlush(false),"replacement World cleared original metabolism cache");
    }
    public static void MetabolismFailedWriteThenRetry(EntityPlayer actualPlayer)
    {
        RequireDisposableSave();
        Need(ThreadManager.IsMainThread(),"native main thread required");
        var state=RebirthMetabolismStateRepository.GetOrCreate(actualPlayer);Need(state!=null,"actual repository state required");
        Need(RebirthMetabolismStateRepository.TryFlush("scenario-seed"),"initial actual full save failed");
        state.Touch();
        string path=Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","Metabolism","players.xml");
        using(var held=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {
            Need(!RebirthMetabolismStateRepository.TryFlush("scenario-locked-primary"),"locked actual publication unexpectedly succeeded");
            Need(!RebirthMetabolismStateRepository.TryResetAfterFlush(false),"failed write cleared dirty original");
            Need(ReferenceEquals(state,RebirthMetabolismStateRepository.GetOrCreate(actualPlayer))&&state.Dirty,"failed write replaced or cleaned original");
        }
        Need(RebirthMetabolismStateRepository.TryFlush("scenario-real-retry"),"actual unlocked retry failed");
        Need(!state.Dirty&&RebirthMetabolismStateRepository.TryResetAfterFlush(false),"positive full save cannot retire original");
    }
}
