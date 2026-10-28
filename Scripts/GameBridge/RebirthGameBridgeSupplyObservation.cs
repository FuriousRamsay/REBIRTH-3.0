using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Explicit local Bridge diagnostic only. Reads the native cache/loaded entity/nav
// separately; absence from the loaded world alone never proves crate removal.
public sealed class ConsoleCmdRebirthSupplyObservation : ConsoleCmdAbstract
{
    public override string[] getCommands() => new[] { "rbsupplytestobserve" };
    public override string getDescription() => "Read one native supply crate's arrival/removal evidence.";
    public override string getHelp() => "rbsupplytestobserve <positive entity id> (read-only, local Game Bridge only).";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        int id;
        var world=GameManager.Instance?.World;
        var player=world?.GetPrimaryPlayer() as EntityPlayerLocal;
        if(args.Count!=1||!int.TryParse(args[0],out id)||id<=0||sender.RemoteClientInfo!=null||
           world==null||world.IsRemote()||player==null||!RebirthGameBridge.IsRunning)
        {SdtdConsole.Instance.Output("*** ERROR "+getHelp());return;}
        var director=world.aiDirector?.GetComponent<AIDirectorAirDropComponent>();
        var crate=world.GetEntity(id) as EntitySupplyCrate;
        var rows=new JArray();bool cached=false,observer=false;int examined=0;
        if(director!=null)
            foreach(var cache in director.supplyCrates)
            {
                if(examined++>=64)break;
                if(cache.entityId!=id)continue;
                cached=true;observer=cache.ChunkObserver!=null;
                rows.Add(new JArray(cache.blockPos.x,cache.blockPos.y,cache.blockPos.z));
            }
        string key=RebirthPurgeKillContributor.Identify(player,world);
        Guid nativeWorld;RebirthPurgeSupplyCrateStamp stamp=null;
        bool stamped=crate!=null&&Guid.TryParseExact(world.worldState?.Guid,"N",out nativeWorld)&&
            RebirthPurgeSupplyCrateSerialization.TryReadAuthoritative(crate,nativeWorld,out stamp);
        bool navAvailable=NavObjectManager.HasInstance;
        var nav=navAvailable?NavObjectManager.Instance.GetNavObjectByEntityID(id):null;
        var result=new JObject {
            ["schema"]=1,["observationOnly"]=true,["world"]=world.worldState?.Guid,
            ["playerEntityId"]=player.entityId,["entityId"]=id,
            ["directorAvailable"]=director!=null,["activeFlight"]=director?.activeAirDrop!=null,
            ["cacheTruncated"]=director!=null&&director.supplyCrates.Count>64,
            ["cached"]=cached,["cachePositions"]=rows,["observerPresent"]=observer,
            ["loaded"]=crate!=null,["grounded"]=crate!=null&&crate.onGround,
            ["stampAuthoritative"]=stamped,["ownedByObservedPlayer"]=stamped&&stamp.Player==key,
            ["stampSequence"]=stamped?stamp.Sequence:0,
            ["navIndexAvailable"]=navAvailable,
            ["entityNavPresent"]=nav!=null,
            ["navDisplayName"]=nav?.DisplayName??string.Empty,
            ["navTrackType"]=nav==null?string.Empty:nav.TrackType.ToString()
        };
        if(crate!=null)
        {
            result["position"]=new JArray(crate.position.x,crate.position.y,crate.position.z);
            result["ownerDisplayName"]=crate.spawnByName??string.Empty;
        }
        var accounts=RebirthPurgeSupplyService.Published;RebirthPurgeSupplyAccount account;
        result["supplyReady"]=key!=null&&accounts!=null&&accounts.TryGetValue(key,out account);
        if(key!=null&&accounts!=null&&accounts.TryGetValue(key,out account))
            result["supply"]=new JObject {
                ["earnedDrops"]=account.EarnedDrops,["deliveredDrops"]=account.DeliveredDrops,
                ["spentCredits"]=account.SpentCredits,["inFlight"]=account.InFlight
            };
        SdtdConsole.Instance.Output(result.ToString(Formatting.None));
    }
}
