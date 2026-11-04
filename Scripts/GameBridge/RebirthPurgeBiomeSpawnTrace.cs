#if PURGE_QUALIFICATION
using System;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json.Linq;

// Observes the final predicate used by the native biome selection transpiler.
// No spawn, allowance, class, policy or test-setup state is changed here.
internal static class RebirthPurgeBiomeSpawnTrace
{
    private static WeakReference<World> owner;
    private static bool installed;
    private static long selected, zombies, suppressedZombies, animals, suppressedAnimals, other;
    private static readonly Dictionary<int,long> classes = new Dictionary<int,long>();
    private static string installationFailure;

    private static void Record(int id, SpawnManagerBiomes manager, bool suppressed)
    {
        if (!RebirthPurgeFeedbackTrace.Allowed || manager?.world == null || manager.world.IsRemote()) return;
        if (owner == null || !owner.TryGetTarget(out var prior) || !ReferenceEquals(prior,manager.world))
        {
            owner=new WeakReference<World>(manager.world);classes.Clear();
            selected=zombies=suppressedZombies=animals=suppressedAnimals=other=0;
        }
        selected++;
        var definition=EntityClass.GetEntityClass(id);
        if (definition != null && definition.bIsAnimalEntity)
        { animals++;if(suppressed)suppressedAnimals++; }
        else if (definition?.classname != null && typeof(EntityZombie).IsAssignableFrom(definition.classname))
        { zombies++;if(suppressed)suppressedZombies++; }
        else other++;
        if(classes.ContainsKey(id))classes[id]++;
        else if(classes.Count<32)classes.Add(id,1);
    }

    internal static JObject Observe(World world)
    {
        if(!installed && installationFailure==null && RebirthPurgeFeedbackTrace.Allowed)
        {
            try
            {
                RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.bridge.purge.biome-observation"),typeof(Hook));
                installed=true;
            }
            catch(Exception error){installationFailure=error.GetType().Name;}
        }
        bool current=world!=null && owner!=null && owner.TryGetTarget(out var prior) && ReferenceEquals(prior,world);
        var rows=new JArray();if(current)foreach(var row in classes)rows.Add(new JObject { ["classId"]=row.Key,["selected"]=row.Value });
        return new JObject {
            ["observationOnly"]=true,["installed"]=installed,["installationFailure"]=installationFailure,
            ["selected"]=current?selected:0,["zombiesSelected"]=current?zombies:0,
            ["zombiesSuppressed"]=current?suppressedZombies:0,["animalsSelected"]=current?animals:0,
            ["animalsSuppressed"]=current?suppressedAnimals:0,["otherSelected"]=current?other:0,
            ["classCapacity"]=32,["classes"]=rows,
            ["scope"]="Final native selection predicate decisions since first observation in this world; allowed is not proof that asynchronous entity creation completed."
        };
    }

    [HarmonyPatch(typeof(RebirthPurgeBiomeZombieSpawnPatch),nameof(RebirthPurgeBiomeZombieSpawnPatch.ShouldSuppress))]
    private static class Hook
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(int entityClassId,SpawnManagerBiomes manager,bool __result)
        { try{Record(entityClassId,manager,__result);}catch{/* Observation must not interrupt spawning. */} }
    }
}
#endif
