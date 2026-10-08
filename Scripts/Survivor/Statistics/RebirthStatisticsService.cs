using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;

#nullable disable

public static class RebirthStatisticsService
{
    private sealed class RuntimeState
    {
        public float LastRealTime;
        public bool HasPosition;
        public Vector3 LastPosition;
        public bool WasDead;
        public float LastCombat;
        public float LastCraft;
        public float LastManagement;
        public float LastSnapshotSend;
    }

    private static readonly Harmony HarmonyInstance = new Harmony("rebirth.survivor.statistics.3.1");
    private static readonly Dictionary<int, RuntimeState> Runtime = new Dictionary<int, RuntimeState>();
    private static readonly List<RebirthStatisticsMilestoneDefinition> Milestones = new List<RebirthStatisticsMilestoneDefinition>();
    private static bool installed;
    private static float nextTick;
    private const float TickSeconds = 1f;
    private const float ActivityWindowSeconds = 8f;
    private const float MaxSampleDistanceMeters = 50f;
    private const int MaxDailyBuckets = 30;

    public static string Install()
    {
        if (installed) return "[REBIRTH Statistics] already installed";
        LoadMilestones();
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthStatisticsHarvestCollectionPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthStatisticsQuestCompletionPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthStatisticsWorldSavePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthStatisticsPlayerDisconnectPatch));
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        installed = true;
        return "[REBIRTH Statistics] installed milestones=" + Milestones.Count;
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data)
    {
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        ResetRuntime();
        RebirthStatisticsRepository.Reset(c != null && c.IsServer);
        RebirthStatisticsClientState.Reset();
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        RebirthStatisticsRepository.SaveAllDirty("world-shutting-down");
        ResetRuntime();
        RebirthStatisticsClientState.Reset();
    }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    {
        RebirthStatisticsRepository.SaveAllDirty("game-shutdown");
        ResetRuntime();
        RebirthStatisticsClientState.Reset();
    }

    private static void ResetRuntime() { Runtime.Clear(); nextTick = 0f; }
    public static void ResetRuntimeForPlayer(EntityPlayer player) { if (player != null) Runtime.Remove(player.entityId); }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        RebirthStatisticsClientState.PumpNotifications();
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c == null || !c.IsServer || !RebirthStatisticsRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.Players == null || world.Players.list == null) return;
        float now = Time.realtimeSinceStartup;
        if (now < nextTick) return;
        nextTick = now + TickSeconds;
        List<EntityPlayer> players = world.Players.list;
        for (int i=0;i<players.Count;i++) TickPlayer(players[i], now);
        Prune(players);
    }

    private static void TickPlayer(EntityPlayer player, float now)
    {
        if (player == null || RebirthCharacterCreationHoldService.IsHeld(player)) return;
        RebirthWorldCharacterRecord character;
        if (!RebirthWorldCharacterService.TryGet(player, out character) || character == null || !character.IsComplete) return;
        RebirthStatisticsRecord record = RebirthStatisticsRepository.GetOrCreate(player);
        if (record == null) return;
        RuntimeState state;
        if (!Runtime.TryGetValue(player.entityId, out state) || state == null)
        {
            state = new RuntimeState { LastRealTime = now, LastPosition = player.position, HasPosition = true, WasDead = player.IsDead() };
            Runtime[player.entityId] = state;
            if (!player.IsDead() && record.DistinctAliveWorldDays == 0)
            {
                record.DistinctAliveWorldDays = 1;
                record.LastObservedAliveWorldDay = GetWorldDay(player);
                record.Touch("alive-day-initialized");
            }
            RebirthStatisticsNativeCounterReader.Merge(record, player);
            EvaluateMilestones(record, GetWorldDay(player));
            SendSnapshot(player, 0L, true, "statistics-runtime-init");
            return;
        }
        float elapsed = Mathf.Clamp(now - state.LastRealTime, 0f, 3f);
        state.LastRealTime = now;
        if (elapsed <= 0f) return;

        bool dead = player.IsDead();
        bool changed = false;
        if (!dead)
        {
            record.ActivePlaySeconds += elapsed;
            record.CurrentLifeSeconds += elapsed;
            if (record.CurrentLifeSeconds > record.LongestLifeSeconds) record.LongestLifeSeconds = record.CurrentLifeSeconds;
            int day = GetWorldDay(player);
            if (day > 0 && day > record.LastObservedAliveWorldDay)
            {
                record.LastObservedAliveWorldDay = day;
                record.DistinctAliveWorldDays++;
            }
            RebirthStatisticsDailyBucket bucket = GetDaily(record, day);
            bucket.ActiveSeconds += elapsed;

            float moved = state.HasPosition ? HorizontalDistance(state.LastPosition, player.position) : 0f;
            bool validMove = moved >= 0.02f && moved <= MaxSampleDistanceMeters;
            if (validMove)
            {
                record.DistanceMeters += moved; bucket.DistanceMeters += moved;
                if (!(player.AttachedToEntity is EntityVehicle)) { record.OnFootMeters += moved; bucket.OnFootMeters += moved; if (bucket.OnFootMeters > record.BestOnFootDistanceInDay) record.BestOnFootDistanceInDay = bucket.OnFootMeters; }
            }
            state.LastPosition = player.position; state.HasPosition = true;
            AccumulateActivity(record, state, bucket, now, elapsed, validMove);
            SampleSurvival(player, bucket);
            TrackLocationAndBiome(player, record);
            changed = true;
        }

        if (dead && !state.WasDead)
        {
            record.Deaths++;
            if (record.CurrentLifeSeconds > record.LongestLifeSeconds) record.LongestLifeSeconds = record.CurrentLifeSeconds;
            record.CurrentLifeSeconds = 0d;
            changed = true;
        }
        else if (!dead && state.WasDead)
        {
            record.CurrentLifeSeconds = 0d;
            state.LastPosition = player.position;
            state.HasPosition = true;
            changed = true;
        }
        state.WasDead = dead;

        changed |= RebirthStatisticsNativeCounterReader.Merge(record, player);
        if (EvaluateMilestones(record, GetWorldDay(player))) changed = true;
        if (changed) record.Touch("statistics-active-sample");
        if (now - state.LastSnapshotSend >= 5f)
        {
            state.LastSnapshotSend = now;
            SendSnapshot(player, 0L, false, "statistics-periodic");
        }
    }

    private static void AccumulateActivity(RebirthStatisticsRecord record, RuntimeState state, RebirthStatisticsDailyBucket bucket, float now, float elapsed, bool moved)
    {
        if (record == null || state == null || bucket == null || elapsed <= 0f) return;
        if (now - state.LastCombat <= ActivityWindowSeconds) { bucket.CombatSeconds += elapsed; record.CombatSeconds += elapsed; }
        else if (now - state.LastCraft <= ActivityWindowSeconds) { bucket.CraftingSeconds += elapsed; record.CraftingSeconds += elapsed; }
        else if (now - state.LastManagement <= ActivityWindowSeconds) { bucket.ManagementSeconds += elapsed; record.ManagementSeconds += elapsed; }
        else if (moved) { bucket.ExplorationSeconds += elapsed; record.ExplorationSeconds += elapsed; }
        else { bucket.SurvivalSeconds += elapsed; record.SurvivalSeconds += elapsed; }
    }

    private static void SampleSurvival(EntityPlayer player, RebirthStatisticsDailyBucket bucket)
    {
        if (player == null || bucket == null) return;
        float hp=0f,sp=0f;
        if (player.Stats != null)
        {
            if (player.Stats.Health != null && player.Stats.Health.Max > 0f) hp = Mathf.Clamp01(player.Stats.Health.Value / player.Stats.Health.Max);
            if (player.Stats.Stamina != null && player.Stats.Stamina.Max > 0f) sp = Mathf.Clamp01(player.Stats.Stamina.Value / player.Stats.Stamina.Max);
        }
        RebirthMetabolismSnapshot m = RebirthMetabolismService.BuildSnapshot(player);
        float nutrition = m.FoodMax > 0f ? Mathf.Clamp01(m.Food / m.FoodMax) : 0f;
        float hydration = m.HydrationMax > 0f ? Mathf.Clamp01(m.Hydration / m.HydrationMax) : 0f;
        float energy = m.EnergyMax > 0f ? Mathf.Clamp01(m.Energy / m.EnergyMax) : 0f;
        bucket.HealthPercentSum += hp; bucket.StaminaPercentSum += sp; bucket.NutritionPercentSum += nutrition; bucket.HydrationPercentSum += hydration; bucket.EnergyPercentSum += energy; bucket.SurvivalSamples++;
    }

    private static void TrackLocationAndBiome(EntityPlayer player, RebirthStatisticsRecord record)
    {
        if (player == null || player.world == null || record == null) return;
        try
        {
            PrefabInstance poi = player.world.GetPOIAtPosition(player.position);
            if (poi != null)
            {
                Vector2 center = poi.GetCenterXZ();
                string name = poi.prefab != null ? (poi.prefab.LocalizedName ?? string.Empty) : string.Empty;
                string key = name + "@" + Mathf.RoundToInt(center.x) + "," + Mathf.RoundToInt(center.y);
                if (record.LocationsDiscovered.Count < 4096) record.LocationsDiscovered.Add(key);
            }
        } catch { }
        try
        {
            BiomeDefinition biome = player.world.GetBiomeInWorld((int)player.position.x, (int)player.position.z);
            if (biome != null && record.BiomesVisited.Count < 64) record.BiomesVisited.Add(biome.m_BiomeType.ToString());
        } catch { }
    }

    public static void MarkCombat(EntityPlayer player)
    {
        RuntimeState s; if(player!=null && Runtime.TryGetValue(player.entityId,out s)&&s!=null)s.LastCombat=Time.realtimeSinceStartup;
    }
    public static void MarkCrafting(EntityPlayer player)
    {
        RuntimeState s; if(player!=null && Runtime.TryGetValue(player.entityId,out s)&&s!=null)s.LastCraft=Time.realtimeSinceStartup;
    }
    public static void MarkManagement(EntityPlayer player)
    {
        RuntimeState s; if(player!=null && Runtime.TryGetValue(player.entityId,out s)&&s!=null)s.LastManagement=Time.realtimeSinceStartup;
    }

    public static void RecordKnowledgeDiscovered(EntityPlayer player, string knowledgeId)
    {
        RebirthStatisticsRecord r=RebirthStatisticsRepository.GetOrCreate(player); if(r==null)return; r.KnowledgeDiscovered++; r.Touch("knowledge:"+(knowledgeId??string.Empty)); EvaluateMilestones(r,GetWorldDay(player)); SendSnapshot(player,0L,false,"knowledge");
    }
    public static void RecordSkillProgress(EntityPlayer player,string skillId,float amount)
    {
        if(amount<=0f)return; RebirthStatisticsRecord r=RebirthStatisticsRepository.GetOrCreate(player);if(r==null)return;r.SkillProgressGained+=amount;r.Touch("skill-progress:"+(skillId??string.Empty));EvaluateMilestones(r,GetWorldDay(player));
    }
    public static void RecordItemsCrafted(EntityPlayer player,string itemOrRecipe,int count)
    {
        if(count<=0)return; RebirthStatisticsRecord r=RebirthStatisticsRepository.GetOrCreate(player);if(r==null)return;r.ItemsCrafted+=count;RebirthStatisticsDailyBucket dayBucket=GetDaily(r,GetWorldDay(player));dayBucket.ItemsCrafted+=count;if(dayBucket.ItemsCrafted>r.BestItemsCraftedInDay)r.BestItemsCraftedInDay=dayBucket.ItemsCrafted;MarkCrafting(player);r.Touch("crafted:"+(itemOrRecipe??string.Empty));EvaluateMilestones(r,GetWorldDay(player));
    }
    public static void RecordResourcesGathered(EntityPlayer player,string itemName,int count)
    {
        if(count<=0)return;RebirthStatisticsRecord r=RebirthStatisticsRepository.GetOrCreate(player);if(r==null)return;r.ResourcesGathered+=count;RebirthStatisticsDailyBucket dayBucket=GetDaily(r,GetWorldDay(player));dayBucket.ResourcesGathered+=count;if(dayBucket.ResourcesGathered>r.BestResourcesInDay)r.BestResourcesInDay=dayBucket.ResourcesGathered;r.Touch("gathered:"+(itemName??string.Empty));EvaluateMilestones(r,GetWorldDay(player));
    }
    public static void RecordPoiCleared(EntityPlayer player, Quest q)
    {
        if(player==null||q==null)return;RebirthStatisticsRecord r=RebirthStatisticsRepository.GetOrCreate(player);if(r==null)return;r.PoisCleared++;string name=q.GetPOIName();Vector3 p=q.GetLocation();if(r.LocationsDiscovered.Count<4096)r.LocationsDiscovered.Add((name??string.Empty)+"@"+Mathf.RoundToInt(p.x)+","+Mathf.RoundToInt(p.z));r.Touch("poi-cleared:"+(name??string.Empty));EvaluateMilestones(r,GetWorldDay(player));
    }
    public static void RecordTraderVisited(EntityPlayer player)
    {
        if(player==null)return;RebirthStatisticsRecord r=RebirthStatisticsRepository.GetOrCreate(player);if(r==null)return;string key="";try{PrefabInstance p=player.world!=null?player.world.GetPOIAtPosition(player.position):null;if(p!=null){Vector2 c=p.GetCenterXZ();key=Mathf.RoundToInt(c.x)+","+Mathf.RoundToInt(c.y);}}catch{}if(key.Length>0&&r.TradersVisited.Count<128)r.TradersVisited.Add(key);MarkManagement(player);r.Touch("trader-interaction");EvaluateMilestones(r,GetWorldDay(player));
    }

    public static void RecordDamage(EntityAlive target, DamageResponse response, DamageSource source, float rawStrength, float actualHealthLoss, float authoritativeBlockedDamage)
    {
        if(target==null||target.world==null||target.world.IsRemote()||source==null)return;
        EntityPlayer attacker=target.world.GetEntity(source.getEntityId()) as EntityPlayer;
        if(attacker!=null && attacker!=target)
        {
            RebirthStatisticsRecord a=RebirthStatisticsRepository.GetOrCreate(attacker);
            if(a!=null)
            {
                double loss=Math.Max(0f,actualHealthLoss);a.DamageDealt+=loss;if(loss>a.HighestDamageHit)a.HighestDamageHit=loss;MarkCombat(attacker);
                bool killed=target.IsDead();
                if(killed)
                {
                    if(target is EntityZombie){a.ZombiesKilled++;RebirthStatisticsDailyBucket dayBucket=GetDaily(a,GetWorldDay(attacker));dayBucket.ZombiesKilled++;if(dayBucket.ZombiesKilled>a.BestZombiesInDay)a.BestZombiesInDay=dayBucket.ZombiesKilled;}
                    else if(target is EntityPlayer)a.PlayerKills++;
                    else if(target is EntityAnimal || target is EntityEnemyAnimal)a.AnimalsKilled++;
                    if((response.HitBodyPart & EnumBodyPartHit.Head)!=EnumBodyPartHit.None)a.HeadshotKills++;
                    string skill=""; if(source.AttackingItem!=null){skill=RebirthProgressionRuntimeConfig.ClassifyCombat(source.AttackingItem);string item=source.AttackingItem.ItemClass!=null?source.AttackingItem.ItemClass.GetItemName():string.Empty;if(item.Length>0){long v;a.WeaponKills.TryGetValue(item,out v);a.WeaponKills[item]=v+1;}}
                    if(IsMeleeSkill(skill))a.MeleeKills++; else if(IsRangedSkill(skill))a.RangedKills++;
                }
                a.Touch("combat-damage"); EvaluateMilestones(a,GetWorldDay(attacker));
            }
        }
        EntityPlayer victim=target as EntityPlayer;
        if(victim!=null && (actualHealthLoss>0f || authoritativeBlockedDamage>0f))
        {
            RebirthStatisticsRecord v=RebirthStatisticsRepository.GetOrCreate(victim);if(v!=null){if(actualHealthLoss>0f)v.DamageTaken+=actualHealthLoss;if(authoritativeBlockedDamage>=0f){v.DamageBlocked+=authoritativeBlockedDamage;v.HasAuthoritativeBlockedDamage=true;}if(actualHealthLoss>0f&&source.GetDamageType()==EnumDamageTypes.Falling&&!victim.IsDead()&&actualHealthLoss>v.HighestFallSurvived)v.HighestFallSurvived=actualHealthLoss;v.Touch("damage-taken");}
        }
    }

    private static bool IsMeleeSkill(string id){return RebirthWeaponFamilySkillService.IsMeleeSkill(id);}
    private static bool IsRangedSkill(string id){return RebirthWeaponFamilySkillService.IsRangedSkill(id);}

    public static RebirthStatisticsSnapshot BuildSnapshot(EntityPlayer player, RebirthStatisticsRecord r)
    {
        RebirthStatisticsSnapshot s=new RebirthStatisticsSnapshot();if(r==null)return s;s.Epoch=r.Epoch;s.Revision=r.Revision;s.PlayerLevel=player!=null&&player.Progression!=null?player.Progression.Level:1;s.ActivePlaySeconds=r.ActivePlaySeconds;s.CurrentLifeSeconds=r.CurrentLifeSeconds;s.LongestLifeSeconds=r.LongestLifeSeconds;s.DaysSurvived=r.DistinctAliveWorldDays;s.DistanceMeters=r.DistanceMeters;s.OnFootMeters=r.OnFootMeters;s.ZombiesKilled=r.ZombiesKilled;s.PlayerKills=r.PlayerKills;s.Deaths=r.Deaths;s.AnimalsKilled=r.AnimalsKilled;s.HeadshotKills=r.HeadshotKills;s.MeleeKills=r.MeleeKills;s.RangedKills=r.RangedKills;s.KnowledgeDiscovered=r.KnowledgeDiscovered;s.SkillProgressGained=r.SkillProgressGained;s.ItemsCrafted=r.ItemsCrafted;s.ResourcesGathered=r.ResourcesGathered;s.PoisCleared=r.PoisCleared;s.LocationsDiscovered=r.LocationsDiscovered.Count;s.TradersVisited=r.TradersVisited.Count;s.BiomesVisited=r.BiomesVisited.Count;s.DamageDealt=r.DamageDealt;s.DamageTaken=r.DamageTaken;s.DamageBlocked=r.DamageBlocked;s.HasAuthoritativeBlockedDamage=r.HasAuthoritativeBlockedDamage;s.HighestDamageHit=r.HighestDamageHit;s.HighestFallSurvived=r.HighestFallSurvived;s.BestZombiesInDay=r.BestZombiesInDay;s.BestItemsCraftedInDay=r.BestItemsCraftedInDay;s.BestResourcesInDay=r.BestResourcesInDay;s.BestOnFootDistanceInDay=r.BestOnFootDistanceInDay;s.CombatSeconds=r.CombatSeconds;s.SurvivalSeconds=r.SurvivalSeconds;s.CraftingSeconds=r.CraftingSeconds;s.ExplorationSeconds=r.ExplorationSeconds;s.ManagementSeconds=r.ManagementSeconds;
        foreach(var kv in r.WeaponKills.OrderByDescending(k=>k.Value).ThenBy(k=>k.Key,StringComparer.OrdinalIgnoreCase).Take(RebirthStatisticsProtocol.MaxWeaponEntries))s.Weapons.Add(new RebirthStatisticsWeaponSnapshot{ItemName=kv.Key,Kills=kv.Value});
        foreach(var kv in r.MilestoneWorldDays.OrderByDescending(k=>k.Value).ThenBy(k=>k.Key,StringComparer.OrdinalIgnoreCase).Take(RebirthStatisticsProtocol.MaxMilestones)){RebirthStatisticsMilestoneDefinition d=Milestones.FirstOrDefault(x=>string.Equals(x.Id,kv.Key,StringComparison.OrdinalIgnoreCase));s.Milestones.Add(new RebirthStatisticsMilestoneSnapshot{Id=kv.Key,Name=d!=null?L(d.NameKey,d.Id):kv.Key,WorldDay=kv.Value});}
        foreach(var d in r.Daily.OrderByDescending(x=>x.WorldDay).Take(RebirthStatisticsProtocol.MaxTrendDays).Reverse()){float inv=d.SurvivalSamples>0?1f/d.SurvivalSamples:0f;s.Trend.Add(new RebirthStatisticsTrendSnapshot{WorldDay=d.WorldDay,HealthPercent=(float)d.HealthPercentSum*inv,StaminaPercent=(float)d.StaminaPercentSum*inv,NutritionPercent=(float)d.NutritionPercentSum*inv,HydrationPercent=(float)d.HydrationPercentSum*inv,EnergyPercent=(float)d.EnergyPercentSum*inv});}
        return s;
    }

    public static bool SendSnapshot(EntityPlayer player,long knownRevision,bool force,string reason)
    { return SendSnapshot(player,0L,knownRevision,force,reason); }
    public static bool SendSnapshot(EntityPlayer player,long knownEpoch,long knownRevision,bool force,string reason)
    {
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c==null||!c.IsServer||player==null)return false;RebirthStatisticsRecord r=RebirthStatisticsRepository.GetOrCreate(player);if(r==null)return false;if(!force&&knownEpoch==r.Epoch&&knownRevision>=r.Revision)return false;RebirthStatisticsSnapshot s=BuildSnapshot(player,r);EntityPlayerLocal local=player as EntityPlayerLocal;if(local!=null)RebirthStatisticsClientState.Receive(s);else c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthStatisticsSnapshot>().Setup(s),_attachedToEntityId:player.entityId);return true;
    }
    public static void RequestSnapshot(EntityPlayerLocal player,bool force)
    {
        if(player==null)return;long epoch=RebirthStatisticsClientState.KnownEpoch;long known=RebirthStatisticsClientState.KnownRevision;ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c==null)return;if(c.IsServer)SendSnapshot(player,epoch,known,force,"local-statistics-request");else c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthStatisticsRequest>().Setup(player.entityId,epoch,known,force));
    }

    private static RebirthStatisticsDailyBucket GetDaily(RebirthStatisticsRecord r,int day)
    {
        if(day<=0)day=1;for(int i=0;i<r.Daily.Count;i++)if(r.Daily[i]!=null&&r.Daily[i].WorldDay==day)return r.Daily[i];var d=new RebirthStatisticsDailyBucket{WorldDay=day};r.Daily.Add(d);r.Daily.Sort((a,b)=>a.WorldDay.CompareTo(b.WorldDay));while(r.Daily.Count>MaxDailyBuckets)r.Daily.RemoveAt(0);return d;
    }
    private static int GetWorldDay(EntityPlayer p){return p!=null&&p.world!=null?GameUtils.WorldTimeToDays(p.world.GetWorldTime()):0;}
    private static float HorizontalDistance(Vector3 a,Vector3 b){float x=a.x-b.x,z=a.z-b.z;return Mathf.Sqrt(x*x+z*z);}
    private static void Prune(List<EntityPlayer> players){HashSet<int> live=new HashSet<int>();for(int i=0;i<players.Count;i++)if(players[i]!=null)live.Add(players[i].entityId);List<int> rm=new List<int>();foreach(var k in Runtime.Keys)if(!live.Contains(k))rm.Add(k);for(int i=0;i<rm.Count;i++)Runtime.Remove(rm[i]);}

    private static bool EvaluateMilestones(RebirthStatisticsRecord r,int day)
    {
        if(r==null)return false;bool changed=false;for(int i=0;i<Milestones.Count;i++){var d=Milestones[i];if(d==null||r.MilestoneWorldDays.ContainsKey(d.Id))continue;if(GetMetric(r,d.Metric)>=d.Threshold){r.MilestoneWorldDays[d.Id]=Math.Max(1,day);changed=true;}}return changed;
    }
    private static double GetMetric(RebirthStatisticsRecord r,string metric){switch((metric??string.Empty).ToLowerInvariant()){case"zombies_killed":return r.ZombiesKilled;case"days_survived":return r.DistinctAliveWorldDays;case"items_crafted":return r.ItemsCrafted;case"locations_discovered":return r.LocationsDiscovered.Count;case"knowledge_discovered":return r.KnowledgeDiscovered;case"skill_progress":return r.SkillProgressGained;case"resources_gathered":return r.ResourcesGathered;case"distance_meters":return r.DistanceMeters;default:return 0d;}}
    private static void LoadMilestones(){Milestones.Clear();try{string root=RebirthSurvivorDefinitionLoader.ResolveConfigRoot();string path=Path.Combine(root,"statistics.xml");if(!File.Exists(path))return;XDocument doc=XDocument.Load(path);foreach(XElement e in doc.Root.Elements("milestone")){double threshold;double.TryParse((string)e.Attribute("threshold"),NumberStyles.Float,CultureInfo.InvariantCulture,out threshold);string id=(string)e.Attribute("id")??"";string metric=(string)e.Attribute("metric")??"";if(id.Length>0&&metric.Length>0&&threshold>0)Milestones.Add(new RebirthStatisticsMilestoneDefinition{Id=id,NameKey=(string)e.Attribute("name_key")??id,Metric=metric,Threshold=threshold});}}catch(Exception ex){Log.Warning("[REBIRTH Statistics] milestone config load failed: "+ex.Message);}}
    private static string L(string key,string fallback){string v=Localization.Get(key??string.Empty);return string.IsNullOrEmpty(v)||v==key?fallback:v;}
}

public static class RebirthStatisticsNativeCounterReader
{
    private static readonly string[] ZombieNames={"KilledZombies","ZombieKills","zombieKills","NumZombiesKilled"};
    private static readonly string[] PlayerNames={"KilledPlayers","PlayerKills","playerKills","NumPlayersKilled"};
    private static readonly string[] DeathNames={"Deaths","deaths","NumDeaths"};
    public static void InitializeBaseline(RebirthStatisticsRecord r,EntityPlayer p){if(r==null||p==null)return;r.NativeZombieBaseline=Math.Max(0L,ReadLong(p,ZombieNames));r.NativePlayerBaseline=Math.Max(0L,ReadLong(p,PlayerNames));r.NativeDeathBaseline=Math.Max(0L,ReadLong(p,DeathNames));r.NativeBaselineInitialized=true;}
    public static bool Merge(RebirthStatisticsRecord r,EntityPlayer p){if(r==null||p==null)return false;if(!r.NativeBaselineInitialized)InitializeBaseline(r,p);bool changed=false;long z=Math.Max(0L,ReadLong(p,ZombieNames)-r.NativeZombieBaseline);if(z>r.ZombiesKilled){r.ZombiesKilled=z;changed=true;}long pk=Math.Max(0L,ReadLong(p,PlayerNames)-r.NativePlayerBaseline);if(pk>r.PlayerKills){r.PlayerKills=pk;changed=true;}long d=Math.Max(0L,ReadLong(p,DeathNames)-r.NativeDeathBaseline);if(d>r.Deaths){r.Deaths=d;changed=true;}return changed;}
    private static long ReadLong(object o,string[] names){Type t=o.GetType();for(int i=0;i<names.Length;i++){try{PropertyInfo p=t.GetProperty(names[i],BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(p!=null){object v=p.GetValue(o,null);if(v!=null)return Convert.ToInt64(v,CultureInfo.InvariantCulture);}FieldInfo f=t.GetField(names[i],BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null){object v=f.GetValue(o);if(v!=null)return Convert.ToInt64(v,CultureInfo.InvariantCulture);}}catch{}}return 0L;}
}

[HarmonyPatch(typeof(GameUtils), "collectHarvestedItem")]
public static class RebirthStatisticsHarvestCollectionPatch
{
    public sealed class HarvestState { public EntityPlayer Player; public ItemValue Item; public int Before; }
    public static void Prefix(object[] __args, out HarvestState __state)
    {
        __state=null;try{ItemActionData action=null;ItemValue item=null;for(int i=0;i<__args.Length;i++){if(action==null&&__args[i] is ItemActionData)action=(ItemActionData)__args[i];else if(item==null&&__args[i] is ItemValue)item=(ItemValue)__args[i];}EntityPlayer p=action!=null&&action.invData!=null?action.invData.holdingEntity as EntityPlayer:null;if(p==null||item==null)return;__state=new HarvestState{Player=p,Item=item,Before=Count(p,item)};}catch{}
    }
    public static void Postfix(HarvestState __state){if(__state==null||__state.Player==null||__state.Item==null)return;int delta=Count(__state.Player,__state.Item)-__state.Before;if(delta>0)RebirthStatisticsService.RecordResourcesGathered(__state.Player,__state.Item.ItemClass!=null?__state.Item.ItemClass.GetItemName():string.Empty,delta);}
    private static int Count(EntityPlayer p,ItemValue item){int n=0;try{if(p.inventory!=null)n+=p.inventory.GetItemCount(item,false,-1,-1);}catch{}try{if(p.bag!=null)n+=p.bag.GetItemCount(item,-1,-1,false);}catch{}return n;}
}

[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.CompleteQuest))]
public static class RebirthStatisticsQuestCompletionPatch
{
    public static void Prefix(Quest q,out bool __state){__state=q!=null&&q.CurrentState==Quest.QuestState.Completed;}
    public static void Postfix(QuestJournal __instance,Quest q,bool __state){if(__state||q==null||q.CurrentState!=Quest.QuestState.Completed)return;EntityPlayer p=ResolveOwner(__instance);if(p==null)return;string poi=q.GetPOIName();if(!string.IsNullOrEmpty(poi)||q.QuestPrefab!=null)RebirthStatisticsService.RecordPoiCleared(p,q);}
    private static EntityPlayer ResolveOwner(QuestJournal journal){if(journal==null||GameManager.Instance==null||GameManager.Instance.World==null)return null;World w=GameManager.Instance.World;List<EntityPlayer> ps=w.Players!=null?w.Players.list:null;if(ps==null)return null;for(int i=0;i<ps.Count;i++)if(ps[i]!=null&&ReferenceEquals(ps[i].QuestJournal,journal))return ps[i];return null;}
}

[HarmonyPatch(typeof(GameManager), "SaveWorld")]
public static class RebirthStatisticsWorldSavePatch{public static void Prefix(){RebirthStatisticsRepository.SaveAllDirty("world-save");}}
[HarmonyPatch(typeof(GameManager), "PlayerDisconnected")]
public static class RebirthStatisticsPlayerDisconnectPatch{public static void Prefix(ClientInfo _cInfo){if(_cInfo==null||GameManager.Instance==null||GameManager.Instance.World==null)return;EntityPlayer p=GameManager.Instance.World.GetEntity(_cInfo.entityId) as EntityPlayer;if(p!=null)RebirthStatisticsRepository.SaveIfDirty(p,"player-disconnected");}}
