// NOT_RUN. Invoke across a REAL native world replacement; no synthetic World.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

public static class MpCDogHireWorldRetirementScenario
{
    public sealed class Original
    {
        internal World World;internal object Settlement;internal string Directory;
        internal Dictionary<string,byte[]> Files;
    }
    static void Need(bool value,string label){if(!value)throw new InvalidOperationException(label);}
    static void Require(){Need(ThreadManager.IsMainThread()&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest","actual disposable CodexTest main thread required");}
    static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    static IDictionary Table(string name)=>(IDictionary)typeof(RebirthDogLifecycleService).GetField(name,BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    static Dictionary<string,byte[]> Images(string root)=>Directory.GetFiles(root,"*",SearchOption.AllDirectories).ToDictionary(path=>path,path=>File.ReadAllBytes(path),StringComparer.Ordinal);
    public static Original CaptureActualPending(EntityPlayer player,EntityRebirthDogCompanion dog)
    {
        Require();Need(player!=null&&dog!=null&&ReferenceEquals(player.world,GameManager.Instance.World),"actual current owner and dog required");
        var value=Table("HireSettlements")[dog.RebirthRuntimeState.StableId];Need(value!=null&&ReferenceEquals(Field(value,"Player"),player),"actual pending hire required");
        Need(!(bool)Field(value,"Busy")&&!(bool)Field(value,"Terminal"),"quiescent unresolved original required");
        var phases=(IDictionary)Table("HireOwnerFiles")[value];
        foreach(var file in phases.Values)Need((int)Field(file,"Workers")==0,"finish actual save workers before this resource-retirement case");
        string root=(string)Field(value,"Directory");
        return new Original{World=player.world,Settlement=value,Directory=root,Files=Images(root)};
    }
    public static void VerifyAfterActualWorldReplacement(Original original)
    {
        Require();Need(original!=null&&GameManager.Instance.World!=null&&!ReferenceEquals(original.World,GameManager.Instance.World),"perform actual native world replacement first");
        typeof(RebirthDogLifecycleService).GetMethod("RetireReplacedHireWorld",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        Need((bool)Field(original.Settlement,"ObservationRetired")&&!(bool)Field(original.Settlement,"Terminal"),"retirement invented a financial terminal");
        Need(!Table("HireSettlements").Values.Cast<object>().Contains(original.Settlement)&&!Table("HireOwnerBudgets").Contains(original.Settlement)&&!Table("HireOwnerFiles").Contains(original.Settlement),"old session still blocks native cold registration or retains its budget");
        Need(Field(original.Settlement,"World")==null&&Field(original.Settlement,"Player")==null&&Field(original.Settlement,"Dog")==null,"retired native graph retained");
        foreach(string name in new[]{"HireNativeOwners","HireNativeDogs","HireRemoteReadies","HireRemoteTerminals"})
            foreach(var value in Table(name).Values)Need(!ReferenceEquals(Field(value,"World"),original.World),"old native reconstruction observation retained: "+name);
        var after=Images(original.Directory);Need(after.Count==original.Files.Count&&original.Files.All(file=>after.TryGetValue(file.Key,out var bytes)&&bytes.SequenceEqual(file.Value)),"resource retirement changed financial WAL/native saved evidence");
    }
}
