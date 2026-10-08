using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

#nullable disable

public static class RebirthStatisticsRepository
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, RebirthStatisticsRecord> Cache = new Dictionary<string, RebirthStatisticsRecord>(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> BlockedLoads = new Dictionary<string, string>(StringComparer.Ordinal);
    private enum LoadStatus { Missing, Loaded, Corrupt, UnsupportedSchema, IdentityMismatch }
    private static bool serverAuthority;
    public static bool IsServerAuthority { get { return serverAuthority; } }
    public static string RootDirectory
    {
        get
        {
            string save = GameIO.GetSaveGameDir();
            return string.IsNullOrEmpty(save) ? string.Empty : Path.Combine(save, "RebirthData", "Survivor", "Statistics");
        }
    }

    public static void Reset(bool asServer)
    {
        lock (Sync) { Cache.Clear(); BlockedLoads.Clear(); }
        serverAuthority = asServer;
    }

    public static bool TryGet(EntityPlayer player, out RebirthStablePlayerIdentity identity, out RebirthStatisticsRecord record)
    {
        identity = null; record = null;
        if (!serverAuthority || player == null || !RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity) || identity == null) return false;
        return TryGet(identity, out record);
    }

    public static bool TryGet(RebirthStablePlayerIdentity identity, out RebirthStatisticsRecord record)
    {
        string ignored;
        return TryGetDetailed(identity, out record, out ignored) == LoadStatus.Loaded;
    }

    private static LoadStatus TryGetDetailed(RebirthStablePlayerIdentity identity, out RebirthStatisticsRecord record, out string error)
    {
        record = null; error = string.Empty;
        if (!serverAuthority || identity == null || string.IsNullOrEmpty(identity.StorageKey)) { error = "identity unavailable"; return LoadStatus.IdentityMismatch; }
        lock (Sync)
        {
            RebirthStatisticsRecord cached;
            if (Cache.TryGetValue(identity.StorageKey, out cached) && cached != null) { record = cached; return LoadStatus.Loaded; }
            string blocked;
            if (BlockedLoads.TryGetValue(identity.StorageKey, out blocked)) { error = blocked; return LoadStatus.Corrupt; }
        }
        string path = GetPath(identity.StorageKey);
        if (string.IsNullOrEmpty(path)) { error = "storage path unavailable"; return LoadStatus.Corrupt; }
        bool primaryExists = File.Exists(path), backupExists = File.Exists(path + ".bak");
        if (!primaryExists && !backupExists) return LoadStatus.Missing;

        RebirthStatisticsRecord loaded; string primaryError;
        LoadStatus primary = TryLoad(path, identity, out loaded, out primaryError);
        if (primary == LoadStatus.Loaded)
        {
            loaded.MarkPersisted(); lock (Sync) Cache[identity.StorageKey] = loaded; record = loaded; return LoadStatus.Loaded;
        }
        if (primary == LoadStatus.UnsupportedSchema || primary == LoadStatus.IdentityMismatch)
        {
            error = "primary " + primaryError; Block(identity.StorageKey, error); return primary;
        }

        RebirthStatisticsRecord backup; string backupError;
        LoadStatus backupStatus = TryLoad(path + ".bak", identity, out backup, out backupError);
        if (backupStatus == LoadStatus.Loaded)
        {
            backup.MarkPersisted(); lock (Sync) Cache[identity.StorageKey] = backup; record = backup;
            Log.Warning("[REBIRTH Statistics] primary unavailable; using validated backup key=" + identity.StorageKey + " error=" + primaryError);
            return LoadStatus.Loaded;
        }
        error = "primary=" + primaryError + "; backup=" + backupError;
        LoadStatus finalStatus = backupStatus == LoadStatus.UnsupportedSchema ? LoadStatus.UnsupportedSchema : (backupStatus == LoadStatus.IdentityMismatch ? LoadStatus.IdentityMismatch : LoadStatus.Corrupt);
        Block(identity.StorageKey, error);
        Log.Warning("[REBIRTH Statistics] statistics storage blocked pending explicit reset key=" + identity.StorageKey + " error=" + error);
        return finalStatus;
    }

    private static void Block(string storageKey, string error)
    {
        lock (Sync) BlockedLoads[storageKey ?? string.Empty] = error ?? "unrecoverable statistics load";
    }

    public static RebirthStatisticsRecord GetOrCreate(EntityPlayer player)
    {
        RebirthStablePlayerIdentity identity; RebirthStatisticsRecord record;
        if (!serverAuthority || player == null || !RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity) || identity == null) return null;
        string error; LoadStatus status = TryGetDetailed(identity, out record, out error);
        if (status == LoadStatus.Loaded && record != null) return record;
        if (status != LoadStatus.Missing) return null;
        record = CreateFresh(player, identity, 1L, "statistics-created");
        lock (Sync) Cache[record.StablePlayerKey] = record;
        return record;
    }

    private static RebirthStatisticsRecord CreateFresh(EntityPlayer player, RebirthStablePlayerIdentity identity, long epoch, string reason)
    {
        RebirthStatisticsRecord record = new RebirthStatisticsRecord
        {
            Epoch = Math.Max(1L, epoch),
            StablePlayerId = identity.CanonicalId ?? string.Empty,
            StablePlayerKey = identity.StorageKey ?? string.Empty,
            FirstObservedWorldDay = player.world != null ? GameUtils.WorldTimeToDays(player.world.GetWorldTime()) : 0,
            LastObservedAliveWorldDay = player.world != null ? GameUtils.WorldTimeToDays(player.world.GetWorldTime()) : 0
        };
        RebirthStatisticsNativeCounterReader.InitializeBaseline(record, player);
        record.Touch(reason);
        return record;
    }

    public static int SaveAllDirty(string reason)
    {
        if (!serverAuthority) return 0;
        List<RebirthStatisticsRecord> dirty = new List<RebirthStatisticsRecord>();
        lock (Sync)
            foreach (RebirthStatisticsRecord r in Cache.Values) if (r != null && r.Dirty) dirty.Add(r.Clone());
        int saved = 0;
        for (int i=0;i<dirty.Count;i++) if (SaveSnapshot(dirty[i], reason)) saved++;
        return saved;
    }

    public static bool SaveIfDirty(EntityPlayer player, string reason)
    {
        RebirthStablePlayerIdentity identity; RebirthStatisticsRecord record;
        if (!TryGet(player, out identity, out record) || record == null || !record.Dirty) return false;
        return SaveSnapshot(record.Clone(), reason);
    }

    public static bool ResetPlayer(EntityPlayer player, out string message)
    {
        message = string.Empty;
        RebirthStablePlayerIdentity identity;
        if (!serverAuthority || player == null || !RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity) || identity == null)
        { message = "server identity unavailable"; return false; }
        long priorEpoch = 0L;
        lock (Sync) { RebirthStatisticsRecord cached; if (Cache.TryGetValue(identity.StorageKey, out cached) && cached != null) priorEpoch = cached.Epoch; }
        long nextEpoch = Math.Max(priorEpoch + 1L, DateTime.UtcNow.Ticks);
        RebirthStatisticsRecord fresh = CreateFresh(player, identity, nextEpoch, "statistics-reset");
        string path = GetPath(identity.StorageKey); string error;
        if (!RebirthAtomicXmlFile.TryWrite(path, Serialize(fresh), out error)) { message = "reset persistence failed: " + error; return false; }
        fresh.MarkPersisted();
        lock (Sync) { Cache[identity.StorageKey] = fresh; BlockedLoads.Remove(identity.StorageKey); }
        RebirthStatisticsService.ResetRuntimeForPlayer(player);
        message = "statistics reset epoch=" + fresh.Epoch.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static bool SaveSnapshot(RebirthStatisticsRecord snapshot, string reason)
    {
        if (snapshot == null || string.IsNullOrEmpty(snapshot.StablePlayerKey)) return false;
        string path = GetPath(snapshot.StablePlayerKey);
        string error;
        if (!RebirthAtomicXmlFile.TryWrite(path, Serialize(snapshot), out error))
        {
            Log.Warning("[REBIRTH Statistics] save failed reason=" + (reason ?? string.Empty) + " error=" + error);
            return false;
        }
        lock (Sync)
        {
            RebirthStatisticsRecord live;
            if (Cache.TryGetValue(snapshot.StablePlayerKey, out live) && live != null && live.Revision == snapshot.Revision) live.MarkPersisted();
        }
        return true;
    }

    private static string GetPath(string storageKey)
    {
        string root = RootDirectory;
        if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(storageKey)) return string.Empty;
        Directory.CreateDirectory(root);
        return Path.Combine(root, storageKey + ".xml");
    }

    private static XDocument Serialize(RebirthStatisticsRecord r)
    {
        XElement root = new XElement("rebirthStatistics",
            new XAttribute("schema_version", RebirthStatisticsRecord.CurrentSchemaVersion),
            new XAttribute("stable_player_id", r.StablePlayerId ?? string.Empty),
            new XAttribute("stable_player_key", r.StablePlayerKey ?? string.Empty),
            new XAttribute("epoch", r.Epoch),
            new XAttribute("revision", r.Revision),
            new XAttribute("native_baseline_initialized", r.NativeBaselineInitialized),
            new XAttribute("native_zombie_baseline", r.NativeZombieBaseline),
            new XAttribute("native_player_baseline", r.NativePlayerBaseline),
            new XAttribute("native_death_baseline", r.NativeDeathBaseline));
        root.Add(new XElement("totals",
            A("active",r.ActivePlaySeconds),A("life",r.CurrentLifeSeconds),A("longestLife",r.LongestLifeSeconds),
            new XAttribute("aliveDays",r.DistinctAliveWorldDays),new XAttribute("firstDay",r.FirstObservedWorldDay),new XAttribute("lastAliveDay",r.LastObservedAliveWorldDay),
            A("distance",r.DistanceMeters),A("onFoot",r.OnFootMeters),new XAttribute("zombies",r.ZombiesKilled),new XAttribute("players",r.PlayerKills),new XAttribute("deaths",r.Deaths),
            new XAttribute("animals",r.AnimalsKilled),new XAttribute("headshots",r.HeadshotKills),new XAttribute("melee",r.MeleeKills),new XAttribute("ranged",r.RangedKills),
            new XAttribute("knowledge",r.KnowledgeDiscovered),A("skillProgress",r.SkillProgressGained),new XAttribute("crafted",r.ItemsCrafted),new XAttribute("resources",r.ResourcesGathered),
            new XAttribute("pois",r.PoisCleared),A("damageDealt",r.DamageDealt),A("damageTaken",r.DamageTaken),A("damageBlocked",r.DamageBlocked),
            new XAttribute("hasBlocked",r.HasAuthoritativeBlockedDamage),A("highestHit",r.HighestDamageHit),A("highestFall",r.HighestFallSurvived),
            new XAttribute("bestZombiesDay",r.BestZombiesInDay),new XAttribute("bestCraftedDay",r.BestItemsCraftedInDay),new XAttribute("bestResourcesDay",r.BestResourcesInDay),A("bestOnFootDay",r.BestOnFootDistanceInDay),
            A("combatSeconds",r.CombatSeconds),A("survivalSeconds",r.SurvivalSeconds),A("craftingSeconds",r.CraftingSeconds),A("explorationSeconds",r.ExplorationSeconds),A("managementSeconds",r.ManagementSeconds)));
        root.Add(SetElement("locations", r.LocationsDiscovered, 4096));
        root.Add(SetElement("traders", r.TradersVisited, 128));
        root.Add(SetElement("biomes", r.BiomesVisited, 64));
        XElement weapons=new XElement("weapons"); foreach(var kv in r.WeaponKills.OrderBy(k=>k.Key,StringComparer.OrdinalIgnoreCase).Take(128)) weapons.Add(new XElement("weapon",new XAttribute("id",kv.Key),new XAttribute("kills",kv.Value))); root.Add(weapons);
        XElement milestones=new XElement("milestones"); foreach(var kv in r.MilestoneWorldDays.OrderBy(k=>k.Key,StringComparer.OrdinalIgnoreCase).Take(256)) milestones.Add(new XElement("milestone",new XAttribute("id",kv.Key),new XAttribute("day",kv.Value))); root.Add(milestones);
        XElement daily=new XElement("daily");
        for(int i=Math.Max(0,r.Daily.Count-30);i<r.Daily.Count;i++) { var d=r.Daily[i]; if(d==null)continue; daily.Add(new XElement("day",new XAttribute("value",d.WorldDay),A("active",d.ActiveSeconds),A("combat",d.CombatSeconds),A("survival",d.SurvivalSeconds),A("crafting",d.CraftingSeconds),A("exploration",d.ExplorationSeconds),A("management",d.ManagementSeconds),new XAttribute("zombies",d.ZombiesKilled),new XAttribute("crafted",d.ItemsCrafted),new XAttribute("resources",d.ResourcesGathered),A("distance",d.DistanceMeters),A("onFoot",d.OnFootMeters),A("health",d.HealthPercentSum),A("stamina",d.StaminaPercentSum),A("nutrition",d.NutritionPercentSum),A("hydration",d.HydrationPercentSum),A("energy",d.EnergyPercentSum),new XAttribute("samples",d.SurvivalSamples))); }
        root.Add(daily); return new XDocument(root);
    }

    private static LoadStatus TryLoad(string path, RebirthStablePlayerIdentity identity, out RebirthStatisticsRecord r, out string error)
    {
        r=null; error="missing"; if(!File.Exists(path))return LoadStatus.Missing; XDocument doc; if(!RebirthAtomicXmlFile.TryLoad(path,out doc,out error))return LoadStatus.Corrupt;
        try
        {
            XElement root=doc.Root; if(root==null||root.Name.LocalName!="rebirthStatistics") { error="invalid root"; return LoadStatus.Corrupt; }
            int schema=I(root,"schema_version"); if(schema<1||schema>RebirthStatisticsRecord.CurrentSchemaVersion){error="unsupported schema "+schema;return LoadStatus.UnsupportedSchema;}
            string sid=S(root,"stable_player_id"), skey=S(root,"stable_player_key"); if(sid!=identity.CanonicalId||skey!=identity.StorageKey){error="identity mismatch";return LoadStatus.IdentityMismatch;}
            r=new RebirthStatisticsRecord{SchemaVersion=RebirthStatisticsRecord.CurrentSchemaVersion,StablePlayerId=sid,StablePlayerKey=skey,Epoch=schema>=2?Math.Max(1,L(root,"epoch")):1L,Revision=Math.Max(1,L(root,"revision"))};
            if(schema>=2){r.NativeBaselineInitialized=B(root,"native_baseline_initialized");r.NativeZombieBaseline=Math.Max(0,L(root,"native_zombie_baseline"));r.NativePlayerBaseline=Math.Max(0,L(root,"native_player_baseline"));r.NativeDeathBaseline=Math.Max(0,L(root,"native_death_baseline"));}
            else { r.NativeBaselineInitialized=true; r.NativeZombieBaseline=0L; r.NativePlayerBaseline=0L; r.NativeDeathBaseline=0L; }
            XElement t=root.Element("totals"); if(t!=null){r.ActivePlaySeconds=NN(D(t,"active"));r.CurrentLifeSeconds=NN(D(t,"life"));r.LongestLifeSeconds=NN(D(t,"longestLife"));r.DistinctAliveWorldDays=Math.Max(0,I(t,"aliveDays"));r.FirstObservedWorldDay=I(t,"firstDay");r.LastObservedAliveWorldDay=I(t,"lastAliveDay");r.DistanceMeters=NN(D(t,"distance"));r.OnFootMeters=NN(D(t,"onFoot"));r.ZombiesKilled=NL(L(t,"zombies"));r.PlayerKills=NL(L(t,"players"));r.Deaths=NL(L(t,"deaths"));r.AnimalsKilled=NL(L(t,"animals"));r.HeadshotKills=NL(L(t,"headshots"));r.MeleeKills=NL(L(t,"melee"));r.RangedKills=NL(L(t,"ranged"));r.KnowledgeDiscovered=NL(L(t,"knowledge"));r.SkillProgressGained=NN(D(t,"skillProgress"));r.ItemsCrafted=NL(L(t,"crafted"));r.ResourcesGathered=NL(L(t,"resources"));r.PoisCleared=NL(L(t,"pois"));r.DamageDealt=NN(D(t,"damageDealt"));r.DamageTaken=NN(D(t,"damageTaken"));r.DamageBlocked=NN(D(t,"damageBlocked"));r.HasAuthoritativeBlockedDamage=B(t,"hasBlocked");r.HighestDamageHit=NN(D(t,"highestHit"));r.HighestFallSurvived=NN(D(t,"highestFall"));r.BestZombiesInDay=NL(L(t,"bestZombiesDay"));r.BestItemsCraftedInDay=NL(L(t,"bestCraftedDay"));r.BestResourcesInDay=NL(L(t,"bestResourcesDay"));r.BestOnFootDistanceInDay=NN(D(t,"bestOnFootDay"));r.CombatSeconds=NN(D(t,"combatSeconds"));r.SurvivalSeconds=NN(D(t,"survivalSeconds"));r.CraftingSeconds=NN(D(t,"craftingSeconds"));r.ExplorationSeconds=NN(D(t,"explorationSeconds"));r.ManagementSeconds=NN(D(t,"managementSeconds"));}
            LoadSet(root.Element("locations"),r.LocationsDiscovered,4096);LoadSet(root.Element("traders"),r.TradersVisited,128);LoadSet(root.Element("biomes"),r.BiomesVisited,64);
            XElement we=root.Element("weapons"); if(we!=null){var entries=we.Elements("weapon").ToList();if(entries.Count>128)throw new InvalidDataException("weapon entry count exceeds 128");foreach(XElement e in entries){string id=S(e,"id");if(id.Length>0)r.WeaponKills[id]=NL(L(e,"kills"));}}
            XElement ms=root.Element("milestones"); if(ms!=null){var entries=ms.Elements("milestone").ToList();if(entries.Count>256)throw new InvalidDataException("milestone entry count exceeds 256");foreach(XElement e in entries){string id=S(e,"id");if(id.Length>0)r.MilestoneWorldDays[id]=Math.Max(0,I(e,"day"));}}
            XElement ds=root.Element("daily"); if(ds!=null){var entries=ds.Elements("day").ToList();if(entries.Count>30)throw new InvalidDataException("daily entry count exceeds 30");foreach(XElement e in entries){r.Daily.Add(new RebirthStatisticsDailyBucket{WorldDay=I(e,"value"),ActiveSeconds=NN(D(e,"active")),CombatSeconds=NN(D(e,"combat")),SurvivalSeconds=NN(D(e,"survival")),CraftingSeconds=NN(D(e,"crafting")),ExplorationSeconds=NN(D(e,"exploration")),ManagementSeconds=NN(D(e,"management")),ZombiesKilled=NL(L(e,"zombies")),ItemsCrafted=NL(L(e,"crafted")),ResourcesGathered=NL(L(e,"resources")),DistanceMeters=NN(D(e,"distance")),OnFootMeters=NN(D(e,"onFoot")),HealthPercentSum=NN(D(e,"health")),StaminaPercentSum=NN(D(e,"stamina")),NutritionPercentSum=NN(D(e,"nutrition")),HydrationPercentSum=NN(D(e,"hydration")),EnergyPercentSum=NN(D(e,"energy")),SurvivalSamples=Math.Max(0,I(e,"samples"))});}}
            return LoadStatus.Loaded;
        } catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;r=null;return LoadStatus.Corrupt;}
    }

    private static XElement SetElement(string name,IEnumerable<string> values,int cap){XElement x=new XElement(name);foreach(string v in values.OrderBy(v=>v,StringComparer.OrdinalIgnoreCase).Take(cap))x.Add(new XElement("id",new XAttribute("value",v)));return x;}
    private static void LoadSet(XElement root,HashSet<string> set,int cap){if(root==null)return;var entries=root.Elements("id").ToList();if(entries.Count>cap)throw new InvalidDataException(root.Name.LocalName+" entry count exceeds "+cap);foreach(XElement e in entries){string v=S(e,"value");if(v.Length>0)set.Add(v);}}
    private static XAttribute A(string n,double v){return new XAttribute(n,v.ToString("R",CultureInfo.InvariantCulture));}
    private static string S(XElement e,string n){XAttribute a=e!=null?e.Attribute(n):null;return a!=null?(a.Value??string.Empty):string.Empty;}
    private static int I(XElement e,string n){string raw=S(e,n);if(raw.Length==0)return 0;int v;if(!int.TryParse(raw,NumberStyles.Integer,CultureInfo.InvariantCulture,out v))throw new InvalidDataException("invalid integer "+n);return v;}
    private static long L(XElement e,string n){string raw=S(e,n);if(raw.Length==0)return 0L;long v;if(!long.TryParse(raw,NumberStyles.Integer,CultureInfo.InvariantCulture,out v))throw new InvalidDataException("invalid long "+n);return v;}
    private static double D(XElement e,string n){string raw=S(e,n);if(raw.Length==0)return 0d;double v;if(!double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out v)||double.IsNaN(v)||double.IsInfinity(v))throw new InvalidDataException("invalid number "+n);return v;}
    private static bool B(XElement e,string n){string raw=S(e,n);if(raw.Length==0)return false;bool v;if(!bool.TryParse(raw,out v))throw new InvalidDataException("invalid bool "+n);return v;}
    private static double NN(double v){if(v<0d)throw new InvalidDataException("negative numeric value");return v;}
    private static long NL(long v){if(v<0L)throw new InvalidDataException("negative integer value");return v;}
}
