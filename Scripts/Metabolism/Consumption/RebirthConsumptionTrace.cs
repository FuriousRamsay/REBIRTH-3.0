using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

// Diagnostic lease only. Never grants custody, mutates effects or changes transport.
internal static class RebirthConsumptionTrace
{
    private static readonly object sync = new object();
    private static readonly Queue<JObject> events = new Queue<JObject>();
    private static DateTime expires;
    private static int remaining;
    private static long sequence;
    [ThreadStatic] private static string request;
    internal static bool Enabled { get { lock(sync) return remaining > 0 && DateTime.UtcNow < expires; } }
    internal static void Configure(bool enable)
    { lock(sync) { remaining=enable?256:0; expires=enable?DateTime.UtcNow.AddMinutes(2):DateTime.MinValue; if(enable)events.Clear(); } }
    internal static JObject Snapshot()
    { lock(sync) { var rows=new JArray();foreach(var row in events)rows.Add(row.DeepClone());return new JObject{["enabled"]=Enabled,["lastSequence"]=sequence,["events"]=rows}; } }
    internal static JObject ObserveServer(int entityId)
    {
        var world=GameManager.Instance?.World;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||connection==null||!connection.IsServer)
            throw new InvalidOperationException("Consumption server observation requires current authoritative world on main thread.");
        var player=world.GetEntity(entityId) as EntityPlayer;
        if(player==null||!player.isEntityRemote||!ReferenceEquals(player.world,world)||
            !ReferenceEquals(world.GetEntity(player.entityId),player)||player.entityId!=entityId)
            throw new InvalidOperationException("Consumption server observation requires explicit current live remote player entity.");
        var client=connection.Clients?.ForEntityId(entityId);
        if(client==null||client.entityId!=entityId||!ReferenceEquals(connection.Clients.ForEntityId(entityId),client))
            throw new InvalidOperationException("Consumption server observation requires current remote player connection witness.");
        var observation=new JObject{["schema"]=1,["player"]=player.entityId,["remoteWorld"]=false,
            ["remoteEntity"]=true,["connectionWitness"]=true,["utc"]=DateTime.UtcNow.ToString("o"),
            ["toolbelt"]=Images(player.inventory?.ItemGrid.items),["backpack"]=Images(player.bag?.ItemGrid.items),
            ["food"]=player.Stats==null?JValue.CreateNull():new JValue(player.Stats.Food.Value),
            ["water"]=player.Stats==null?JValue.CreateNull():new JValue(player.Stats.Water.Value)};
        var cvars=new JObject();
        foreach(string name in new[]{"infectionCounter","$infectionMaxDuration","$infectionCureCounter","$buffInfectionAddCurePerc"})
            cvars[name]=player.Buffs==null?JValue.CreateNull():new JValue(player.Buffs.GetCustomVar(name));
        observation["cvars"]=cvars;
        observation["infectionCureBuff"]=player.Buffs!=null&&player.Buffs.HasBuff("buffInfectionAddCure");
        var snapshot=Snapshot();
        var scoped=new JArray();
        foreach(JObject row in (JArray)snapshot["events"])
            if((int?)row["player"]==entityId)scoped.Add(row.DeepClone());
        snapshot["events"]=scoped;snapshot["observation"]=observation;
        return snapshot;
    }
    private static JArray Images(ItemStack[] slots)
    {
        if(slots==null)return null;
        var rows=new JArray();
        for(int slot=0;slot<slots.Length;slot++)
        {var row=Image(slots[slot])??new JObject{["count"]=0,["type"]=0,["seed"]=0,["digest"]=null};row["slot"]=slot;rows.Add(row);}
        return rows;
    }
    internal static IDisposable Begin()
    { if(!Enabled)return null;var scope=new Scope(request);request=Guid.NewGuid().ToString("N");return scope; }
    private sealed class Scope : IDisposable
    { private readonly string previous;internal Scope(string value){previous=value;}public void Dispose(){request=previous;} }
    internal static void Note(string stage,EntityPlayer player=null,Guid publication=default(Guid),
        RebirthMetabolismSourceKind kind=default(RebirthMetabolismSourceKind),int slot=-1,
        ItemStack before=null,ItemStack after=null,string reason=null,bool? success=null,ItemValue item=null)
    {
        if(!Enabled)return;
        try
        {
            var row=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["stage"]=stage,
                ["request"]=request,["publication"]=publication==Guid.Empty?null:publication.ToString("N"),
                ["player"]=player?.entityId??-1,["remoteWorld"]=player?.world?.IsRemote()??false,
                ["remoteEntity"]=player?.isEntityRemote??false,["sourceKind"]=kind.ToString(),["slot"]=slot,
                ["before"]=Image(before),["after"]=Image(after),["reason"]=reason};
            if(success.HasValue)row["success"]=success.Value;
            if(item!=null)row["effectItem"]=Image(new ItemStack(item.Clone(),1));
            if(player?.Buffs!=null)
            {
                row["infectionCounter"]=player.Buffs.GetCustomVar("infectionCounter");
                row["infectionCureCounter"]=player.Buffs.GetCustomVar("$infectionCureCounter");
                row["infectionAddCurePerc"]=player.Buffs.GetCustomVar("$buffInfectionAddCurePerc");
                row["infectionCureBuff"]=player.Buffs.HasBuff("buffInfectionAddCure");
                row["eventIsLocal"]=player.MinEventContext.IsLocal;
            }
            lock(sync)
            {
                if(!Enabled)return;
                remaining--;row["sequence"]=++sequence;events.Enqueue(row);while(events.Count>128)events.Dequeue();
            }
            Log.Out("[ConsumptionTrace] "+row.ToString(Newtonsoft.Json.Formatting.None));
        }
        catch { } // Diagnostics must never interfere with consumption.
    }
    private static JObject Image(ItemStack stack)
    {
        if(stack==null)return null;
        bool empty=stack.IsEmpty();
        string digest=null;
        if(!empty)using(var hash=SHA256.Create())digest=Convert.ToBase64String(hash.ComputeHash(
            Encoding.UTF8.GetBytes(RebirthNativeItemCodec.Encode(stack.itemValue.Clone()))));
        return new JObject{["count"]=stack.count,["type"]=empty?0:stack.itemValue.type,
            ["seed"]=empty?0:stack.itemValue.Seed,["digest"]=digest};
    }
}

public sealed class ConsoleCmdRebirthConsumptionTrace : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    public override bool AllowedInMainMenu => false;
    public override int DefaultPermissionLevel => 1000;
    public override string[] getCommands()=>new[]{"rbconsumptiontrace"};
    public override string getDescription()=>"Bounded opt-in consumption diagnostics.";
    public override string getHelp()=>"rbconsumptiontrace on|off|snapshot|observe <remotePlayerEntityId> (observe: read-only current authoritative server entity). on: two minutes, at most256 lines; last128 retained. Enable separately on server and remote owner.";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        if(args.Count==2&&args[0]=="observe"&&int.TryParse(args[1],out var entityId))
        {
            try{SingletonMonoBehaviour<SdtdConsole>.Instance.Output(RebirthConsumptionTrace.ObserveServer(entityId).ToString(Newtonsoft.Json.Formatting.None));}
            catch(Exception error){SingletonMonoBehaviour<SdtdConsole>.Instance.Output("Consumption observation failed: "+error.Message);}
            return;
        }
        if(args.Count==1&&args[0]=="on")RebirthConsumptionTrace.Configure(true);
        else if(args.Count==1&&args[0]=="off")RebirthConsumptionTrace.Configure(false);
        else if(args.Count!=1||args[0]!="snapshot")
        {SingletonMonoBehaviour<SdtdConsole>.Instance.Output(getHelp());return;}
        SingletonMonoBehaviour<SdtdConsole>.Instance.Output(RebirthConsumptionTrace.Snapshot().ToString(Newtonsoft.Json.Formatting.None));
    }
}
