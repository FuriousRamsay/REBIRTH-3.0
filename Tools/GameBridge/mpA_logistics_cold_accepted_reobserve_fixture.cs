using System;
using System.Reflection;
// PREPARED, NOT RUN. Requires a real retained State0 from interrupted native
// accepted-body observation, its original GUID marker and actual loaded body.
// Does not fabricate any positive native publication receipt or body.
internal static class mpA_logistics_cold_accepted_reobserve_fixture
{
    internal static void BeforePublication(Assembly production,object actualOriginal,EntityBackpack actualBody)
    {
        var type=production.GetType("RebirthLogisticsAcceptedBodyJournal",true);
        object Call(string name,params object[] args)=>type.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
        if((bool)Call("HasPublished",actualOriginal))throw new Exception("Scenario requires retained State0, not existing publication.");
        if(!(bool)Call("Holds",actualBody))throw new Exception("Exact retained original marker/body was not held.");
        var args=new object[]{actualOriginal,null};
        if((bool)Call("TryAdopt",args))throw new Exception("Cold State0 became mutation-capable before native publication.");
        Call("Tick",actualOriginal); // readonly capture only; native writer must independently publish.
    }
    internal static void AfterActualPublication(Assembly production,object actualOriginal,EntityBackpack actualBody)
    {
        var type=production.GetType("RebirthLogisticsAcceptedBodyJournal",true);
        object Call(string name,params object[] args)=>type.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
        Call("Tick",actualOriginal);
        if(!(bool)Call("HasPublished",actualOriginal))throw new Exception("Wait for real same-snapshot native region readback before this step.");
        var args=new object[]{actualOriginal,null};if(!(bool)Call("TryAdopt",args))throw new Exception("Positively published exact body could not be adopted.");
        var actual=args[1];var body=actual.GetType().GetField("Backpack",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(actual);
        if(!ReferenceEquals(body,actualBody))throw new Exception("Adoption rebound a different native body.");
    }
}
