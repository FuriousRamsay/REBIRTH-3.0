using System;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

public static partial class RebirthGameBridgeHandlers
{
    private static readonly string AuditProcessIdentity=Guid.NewGuid().ToString("N");
    private sealed class AuditWorldToken{internal readonly string Value=Guid.NewGuid().ToString("N");}
    private static readonly ConditionalWeakTable<World,AuditWorldToken> auditWorldTokens=new ConditionalWeakTable<World,AuditWorldToken>();
    // Observation only: never initializes a character, resets counters or applies state.
    private static void AuditState(BridgeRequest req)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null){req.Fail("Current world required");return;}
        var local=world.GetPrimaryPlayer();
        int id=req.QueryInt("player",local?.entityId??-1);
        var actor=world.GetEntity(id) as EntityPlayer;
        if(actor==null||!ReferenceEquals(actor.world,world)){req.Fail("Current player required");return;}
        if(world.IsRemote()&&!ReferenceEquals(actor,local)){req.Fail("Remote observations are owner-local");return;}
        RebirthWorldCharacterRecord record=null;
        if(!world.IsRemote()&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&identity!=null)
            RebirthWorldCharacterRepository.TryGetCurrentCached(identity,out record);
        var domains=new JObject();
        string selected=req.QueryString("domain",string.Empty);
        Action<string,Func<object>> add=(name,capture)=>
        {
            if(selected.Length!=0&&!string.Equals(selected,name,StringComparison.OrdinalIgnoreCase))return;
            try{var value=capture();domains[name]=value==null?new JObject{["available"]=false}:value is JToken token?token:JToken.FromObject(value);}
            catch(Exception error){domains[name]=new JObject{["available"]=false,["error"]=error.GetType().Name};}
        };
        add("topology",()=>
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var peer=manager?.IsServer==true?manager.Clients?.ForEntityId(actor.entityId):null;
            RebirthStablePlayerIdentity localIdentity=null;
            string ownerKey=null;
            if(world.IsRemote())
            {if(RebirthStablePlayerIdentity.TryFromLocalPlatform(out localIdentity))ownerKey=localIdentity.StorageKey;}
            else
            {
                var persistent=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId);
                string canonical=persistent?.PrimaryId?.CombinedString;
                if(!string.IsNullOrEmpty(canonical))ownerKey=RebirthStablePlayerIdentity.ComputeStorageKey(canonical.Trim());
            }
            string worldKey=world.IsRemote()?GamePrefs.GetString(EnumGamePrefs.GameGuidClient):world.worldState?.Guid;
            if(Guid.TryParse(worldKey,out var worldGuid))worldKey=worldGuid.ToString("N");
            string creation=world.IsRemote()?(RebirthSurvivorMode.IsEnabledForCurrentWorld()?RebirthSurvivorClientState.GetProjectedCreationId(local):RebirthCamperCharacterAccess.CurrentCreation(actor)):
                RebirthSurvivorMode.IsEnabledForCurrentWorld()?record?.Origin?.CreationId:RebirthCamperCharacterAccess.CurrentCreation(actor);
            return new{schema=1,processIdentity=AuditProcessIdentity,worldInstance=auditWorldTokens.GetValue(world,_=>new AuditWorldToken()).Value,worldKey,ownerKey,entityId=actor.entityId,
                creationId=creation,deaths=actor.Died,gameName=GamePrefs.GetString(EnumGamePrefs.GameName),
                remote=world.IsRemote(),server=manager?.IsServer==true,dedicated=GameManager.IsDedicatedServer,
                authoritativePeer=peer!=null&&peer.entityId==actor.entityId&&peer.loginDone,
                localActor=ReferenceEquals(actor,local),connected=manager!=null&&!manager.IsServer&&manager.connectionToServer!=null&&
                    manager.connectionToServer.Length>0&&manager.connectionToServer[0]!=null&&!manager.connectionToServer[0].IsDisconnected()};
        });
        add("trader",()=>RebirthTraderJobListRefresh.CaptureAuditState());
        add("party",()=>RebirthPartyIdentityService.CaptureAuditState());
        add("theory",()=>RebirthTheoryStudySettlement.CaptureAuditState(record));
        add("soloCombat",()=>RebirthTheorySoloCombatService.CaptureAuditState(actor));
        add("combat",()=>RebirthWeaponSustainedDpsService.CaptureAuditState(actor));
        add("rage",()=>RebirthRageService.CaptureAuditState(actor));
        add("blackMagic",()=>RebirthBlackMagicService.CaptureAuditState(actor));
        add("boundUndead",()=>RebirthBoundUndeadService.CaptureAuditState(actor));
        add("explosion",()=>RebirthExplosionWorkWitness.CaptureAuditState(actor));
        add("dogLoot",()=>RebirthDogAutoLootBridge.CaptureAuditState(actor));
        add("sharedQuest",()=>RebirthQuestNotificationPolicy.CaptureAuditState(actor,req.QueryInt("questCode",-1),req.QueryInt("memberId",-1)));
        add("npcProgression",()=>RebirthNpcStableId.TryParse(req.QueryString("npc",string.Empty),out var npc)
            ?RebirthNpcProgressionAdministration.CaptureAuditState(npc):new JObject{["available"]=false});
        add("npcRecovery",()=>RebirthNpcLiveRecoveryRuntime.CaptureAuditState());
        add("npcEquipment",()=>RebirthNpcStableId.TryParse(req.QueryString("npc",string.Empty),out var equipmentNpc)
            ?RebirthNpcNativeEquipmentBridge.CaptureAuditState(equipmentNpc):new JObject{["available"]=false});
        add("creator",()=>XUiC_RebirthSurvivorCreator.CaptureAuditState());
        add("nativeInventory",()=>RebirthAgentCNativeAudit.CaptureInventory(actor));
        add("stamina",()=>RebirthMetabolismService.CaptureStaminaAuditState(actor));
        add("healthCapacity",()=>RebirthHealthCapacityService.CaptureAuditState(actor));
        add("salvage",()=>RebirthSalvageAcceptedHitWitness.CaptureAuditState(actor));
        add("fire",()=>CaptureFireAuditState(world,actor,req));
        add("npcMelee",()=>EAIRebirthMeleeCombat.CaptureAuditState(world.GetEntity(req.QueryInt("entity",-1)) as EntityAlive));
        add("cookingAdmission",()=>RebirthCookingRemoteAdmissionBackend.CaptureAuditState(actor));
        add("stationPackets",()=>RebirthGameBridgeStationPacketProbe.Capture(world,
            new Vector3i(req.QueryInt("x",int.MinValue),req.QueryInt("y",int.MinValue),req.QueryInt("z",int.MinValue))));
        add("stationCommands",()=>RebirthCamperRemoteStationCommandAudit.Capture(actor,req.QueryString("operation",string.Empty)));
        add("hydrationOriginal",()=>RebirthHydrationAudit.Capture(actor,
            Guid.TryParse(req.QueryString("operation",string.Empty),out var hydrationOperation)?hydrationOperation:Guid.Empty,
            req.QueryString("digest",string.Empty)));
        add("cookingPull",()=>RebirthCookingLegacyPull.CapturePullAuditState(actor,Guid.TryParse(req.QueryString("operation",string.Empty),out var cookingPullOperation)?cookingPullOperation:Guid.Empty));
        add("cookingQueue",()=>
        {
            var position=new Vector3i(req.QueryInt("x",int.MinValue),req.QueryInt("y",int.MinValue),req.QueryInt("z",int.MinValue));
            var station=world.GetTileEntity(position) as TileEntityWorkstation;
            if(station?.Queue==null)return new JObject{["available"]=false};
            var rows=new JArray();
            for(int index=0;index<station.Queue.Length;index++)
            {
                var recipe=station.Queue[index]?.Recipe;
                if(recipe==null)continue;
                bool hasBatch=RebirthCookingOutputDelivery.TryGetBatch(recipe,out var batch);
                bool outputPlan=RebirthRecipeOutputs.TryPlan(recipe,Math.Max(1,(int)station.Queue[index].Multiplier),out var extras,out var outputReason);
                var marker=recipe.ingredients.Count>0?recipe.ingredients[0].itemValue:null;
                int admissionPhase=0;
                marker?.TryGetMetadata(RebirthCookingRemoteAdmission.PendingKey,out admissionPhase);
                rows.Add(new JObject{
                    ["index"]=index,["recipe"]=recipe.GetName(),["ready"]=RebirthCookingHeat.Ready(recipe),
                    ["preparedOutput"]=RebirthCookingOutputDelivery.HasPrepared(recipe),
                    ["batch"]=hasBatch?batch.ToString("N"):string.Empty,
                    ["localTakeReceipt"]=hasBatch&&ReferenceEquals(actor,local)?actor.Buffs.GetCustomVar("rbCookingTake_"+batch.ToString("N")):0f,
                    ["portions"]=station.Queue[index].Multiplier,["recipeCount"]=recipe.count,
                    ["markerFields"]=marker?.Metadata?.Count??0,["outputPlan"]=outputPlan,
                    ["outputReason"]=outputReason??string.Empty,["extraOutputs"]=extras?.Count??0,
                    ["primaryOutput"]=RebirthCookingHeat.Output(recipe)?.ItemClass?.GetItemName()??string.Empty,
                    ["admissionPhase"]=admissionPhase
                });
            }
            int uiRows=-1;
            if(ReferenceEquals(actor,local))
            {
                var ui=LocalPlayerUI.GetUIForPlayer(local)?.xui;
                if(ui?.WindowGroups!=null)foreach(var group in ui.WindowGroups)
                    if(group?.Controller is XUiC_RebirthCookingStation cooking&&ReferenceEquals(cooking.WorkstationData?.TileEntity,station))
                    {
                        uiRows=0;
                        foreach(var card in cooking.craftingQueue.GetRecipesToCraft())if(card?.GetRecipe()!=null)uiRows++;
                        break;
                    }
            }
            return new JObject{["available"]=true,["rows"]=rows,["uiRows"]=uiRows,
                ["cookingHeld"]=RebirthCookingRemoteAdmissionBackend.Held(station),
                ["cancellationHeld"]=RebirthCookingRemoteCancellation.Held(station),
                ["hiddenOutputHeld"]=RebirthCookingHiddenOutputRecovery.Held(station),
                ["clientCustodyBlocked"]=RebirthRemoteResourcePlacedStationClientCustody.Blocks(station),
                ["serverCustodyBlocked"]=RebirthRemoteResourcePlacedStationCustody.Blocks(station),
                ["localLock"]=LockManager.Instance?.IsLockedByLocalPlayer(station,0)==true};
        });
        add("playerConsumption",()=>RebirthConsumptionDurableDelivery.CaptureInventoryAuditState(actor,Guid.TryParse(req.QueryString("operation",string.Empty),out var consumptionOperation)?consumptionOperation:Guid.Empty));
        add("restorationOriginal",()=>RebirthVehicleTransferAudit.Capture(actor,Guid.TryParse(req.QueryString("operation",string.Empty),out var restorationOperation)?restorationOperation:Guid.Empty));
        add("remoteResourceOriginals",()=>RebirthRemoteResourceDurableDispatch.CaptureAuditState(actor,Guid.TryParse(req.QueryString("operation",string.Empty),out var resourceOperation)?resourceOperation:Guid.Empty));
        add("remoteBlockOwnerRelease",()=>RebirthRemoteResourceBlockProtocol.CaptureOwnerRelease(actor,Guid.TryParse(req.QueryString("operation",string.Empty),out var blockOwnerOperation)?blockOwnerOperation:Guid.Empty));
        add("removalParcelOriginals",()=>RebirthCamperStationContentsDelivery.CaptureParcelAuditState(actor));
        add("repairOriginals",()=>RebirthRepairOriginalCorrection.CaptureAuditState(actor));
        add("recipeOutputOriginals",()=>RebirthRecipeOutputIntegration.CaptureOutputAuditState(actor));
        add("npcTradeOriginals",()=>RebirthNpcTradeClientFlow.CaptureAuditState(actor));
        add("npcTradeOriginal",()=>Guid.TryParse(req.QueryString("operation",string.Empty),out var ownerTradeOperation)
            ?RebirthNpcTradeOwnerOriginalStore.CaptureAuditState(actor,ownerTradeOperation):new{schema=1,available=false});
        add("npcTradeInventory",()=>RebirthNpcStableId.TryParse(req.QueryString("npc",string.Empty),out var tradeNpc)&&
            Guid.TryParse(req.QueryString("operation",string.Empty),out var tradeOperation)
            ?RebirthNpcInventoryTransactionService.CaptureTradeAuditState(tradeNpc,tradeOperation):new{schema=1,available=false});
        add("placedSource4Originals",()=>RebirthRemoteResourcePlacedStationOriginal.CaptureSource4AuditState(actor));
        add("recyclerPlacedOriginals",()=>RebirthRecyclerPlacedSettlement.CaptureAuditState(actor,
            Guid.TryParse(req.QueryString("operation",string.Empty),out var recyclerPlacedOperation)?recyclerPlacedOperation:Guid.Empty));
        add("logisticsOriginals",()=>RebirthLogisticsOriginalTransport.CaptureAuditState(actor,
            Guid.TryParse(req.QueryString("operation",string.Empty),out var logisticsOperation)?logisticsOperation:Guid.Empty));
        add("sinkOriginal",()=>RebirthCamperSink.CaptureAuditState(actor,req.QueryInt("entity",-1),
            Guid.TryParse(req.QueryString("operation",string.Empty),out var sinkOperation)?sinkOperation:Guid.Empty));
        add("placedOwedOriginals",()=>RebirthPlacedOriginalDeathPartition.CaptureOwedAuditState(actor,
            Guid.TryParse(req.QueryString("operation",string.Empty),out var placedOperation)?placedOperation:Guid.Empty));
        add("waterOriginals",()=>AdvancedFarmingWaterExchangeOriginal.CaptureAuditState(actor,
            Guid.TryParse(req.QueryString("operation",string.Empty),out var operation)?operation:Guid.Empty));
        add("bossOriginals",()=>RebirthBossNativePublication.CaptureAuditState(actor,
            Guid.TryParse(req.QueryString("event",string.Empty),out var bossEvent)?bossEvent:Guid.Empty));
        req.Complete(new JObject{["observationVersion"]=1,["worldId"]=world.worldState?.Guid,
            ["player"]=id,["authority"]=!world.IsRemote(),["domains"]=domains});
    }
}
