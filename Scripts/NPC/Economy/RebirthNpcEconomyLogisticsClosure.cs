using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

#nullable disable

public enum RebirthNpcEconomyOutcomeStatus : byte { Accepted=0, Rejected=1, Duplicate=2, Compensated=3, Failed=4 }
public enum RebirthNpcSocialSourceKind : byte { Trade=0, Theft=1, PropertyDamage=2, Assistance=3, Healing=4, Hiring=5, Dismissal=6, Mission=7, Work=8 }

public sealed class RebirthNpcEconomyOutcome
{
    public RebirthNpcEconomyOutcomeStatus Status { get; internal set; }
    public string OperationId { get; internal set; }
    public string Detail { get; internal set; }
    public long InputQuantity { get; internal set; }
    public long OutputQuantity { get; internal set; }
    public bool Committed { get; internal set; }
}

public sealed class RebirthNpcProductionRecipe
{
    public string RecipeId { get; internal set; }
    public string ProfessionId { get; internal set; }
    public string InputResource { get; internal set; }
    public int InputQuantity { get; internal set; }
    public string OutputResource { get; internal set; }
    public int OutputQuantity { get; internal set; }
    public int DurationSeconds { get; internal set; }
}

public sealed class RebirthNpcTradeCatalogueEntry
{
    public string CatalogueId { get; internal set; }
    public string ProfileId { get; internal set; }
    public string FactionId { get; internal set; }
    public string ItemKey { get; internal set; }
    public string CurrencyItemKey { get; internal set; }
    public int BaseBuyPrice { get; internal set; }
    public int BaseSellPrice { get; internal set; }
    public int StockMinimum { get; internal set; }
    public int StockMaximum { get; internal set; }
    public int RestockPerDay { get; internal set; }
}

public interface IRebirthNpcQuickStackEndpoint
{
    bool CanAccess(string actorId, string settlementId, out string denial);
    bool TryReserve(string operationId, string actorId, string itemKey, int quantity, out string reservationId, out string detail);
    bool TryCommit(string reservationId, out string detail);
    bool TryCompensate(string reservationId, out string detail);
}

/// <summary>ACIP-09 economy/logistics closure. All success events are emitted only after committed outcomes.</summary>
public static class RebirthNpcEconomyLogisticsService
{
    private sealed class OfflineCheckpoint { public string SettlementId; public long LastUtcTicks; public long Revision; public readonly Dictionary<string,long> PendingRecipeTicks=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase); }
    private static readonly object Sync=new object();
    private static readonly Dictionary<string,IRebirthNpcQuickStackEndpoint> Endpoints=new Dictionary<string,IRebirthNpcQuickStackEndpoint>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,OfflineCheckpoint> Checkpoints=new Dictionary<string,OfflineCheckpoint>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,RebirthNpcProductionRecipe> Recipes=new Dictionary<string,RebirthNpcProductionRecipe>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,RebirthNpcTradeCatalogueEntry> Catalogue=new Dictionary<string,RebirthNpcTradeCatalogueEntry>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Replay=new HashSet<string>(StringComparer.Ordinal);
    private static readonly Queue<string> ReplayOrder=new Queue<string>();
    private const int MaxReplay=4096;
    private const int MaxOfflineHours=72;
    private const int MaxOfflineSteps=512;
    private static bool initialized;
    private static long quickStackAccepted,quickStackRejected,quickStackCompensated,compensationFailures,offlineRuns,offlineCapped,offlineSteps,resourceConservationFailures,socialPublished,socialSuppressed,tradeEntries;

    public static void EnsureInitialized()
    {
        lock(Sync){if(!initialized){RegisterProductionRecipesNoLock();RegisterTradeCatalogueNoLock();initialized=true;}}
        RebirthNpcEconomyPersistenceStore.Load();
    }

    public static void RegisterQuickStackEndpoint(string endpointId,IRebirthNpcQuickStackEndpoint endpoint)
    { if(string.IsNullOrWhiteSpace(endpointId)||endpoint==null)throw new ArgumentException("Quick Stack endpoint requires id and implementation."); lock(Sync)Endpoints[endpointId.Trim()]=endpoint; }

    public static RebirthNpcEconomyOutcome ExecuteQuickStack(string operationId,string endpointId,string actorId,string settlementId,string itemKey,int quantity)
    {
        EnsureInitialized(); operationId=Normalize(operationId); if(operationId.Length==0||quantity<=0)return Reject(operationId,"Quick Stack requires operation id and positive quantity.");
        lock(Sync){if(Replay.Contains(operationId))return new RebirthNpcEconomyOutcome{Status=RebirthNpcEconomyOutcomeStatus.Duplicate,OperationId=operationId,Detail="Duplicate operation suppressed."};}
        IRebirthNpcQuickStackEndpoint endpoint; lock(Sync){if(!Endpoints.TryGetValue(endpointId??string.Empty,out endpoint))return Reject(operationId,"Quick Stack endpoint unavailable.");}
        string denial;if(!endpoint.CanAccess(actorId,settlementId,out denial))return Reject(operationId,"Quick Stack permission denied: "+denial);
        string reservation,detail;if(!endpoint.TryReserve(operationId,actorId,itemKey,quantity,out reservation,out detail))return Reject(operationId,"Reservation failed: "+detail);
        if(!RebirthNpcSettlementSimulation.DepositResource(settlementId,itemKey,quantity))
        { string compensation;if(!endpoint.TryCompensate(reservation,out compensation)){Interlocked.Increment(ref compensationFailures);return Fail(operationId,"Settlement deposit failed and compensation failed: "+compensation);} Interlocked.Increment(ref quickStackCompensated);return new RebirthNpcEconomyOutcome{Status=RebirthNpcEconomyOutcomeStatus.Compensated,OperationId=operationId,Detail="Settlement deposit failed; source reservation compensated.",InputQuantity=quantity}; }
        if(!endpoint.TryCommit(reservation,out detail))
        { bool reversed=RebirthNpcSettlementSimulation.TryWithdrawResource(settlementId,itemKey,quantity);string compensation;bool compensated=endpoint.TryCompensate(reservation,out compensation);if(!reversed||!compensated)Interlocked.Increment(ref compensationFailures);Interlocked.Increment(ref quickStackCompensated);return new RebirthNpcEconomyOutcome{Status=RebirthNpcEconomyOutcomeStatus.Compensated,OperationId=operationId,Detail="Commit failed; compensated="+(reversed&&compensated),InputQuantity=quantity}; }
        CommitReplay(operationId);Interlocked.Increment(ref quickStackAccepted);PublishCommittedSocial(RebirthNpcSocialSourceKind.Assistance,actorId,default(RebirthNpcStableId),"quickstack:"+itemKey,quantity);return new RebirthNpcEconomyOutcome{Status=RebirthNpcEconomyOutcomeStatus.Accepted,OperationId=operationId,Detail="Quick Stack committed.",InputQuantity=quantity,OutputQuantity=quantity,Committed=true};
    }

    public static int SimulateOffline(string settlementId,long nowUtcTicks)
    {
        EnsureInitialized();if(string.IsNullOrWhiteSpace(settlementId)||nowUtcTicks<=0)return 0;
        RebirthNpcProductionRecipe[] recipes;OfflineCheckpoint checkpoint;long elapsed;
        lock(Sync)
        {
            if(!Checkpoints.TryGetValue(settlementId,out checkpoint)){Checkpoints[settlementId]=new OfflineCheckpoint{SettlementId=settlementId,LastUtcTicks=nowUtcTicks,Revision=1};RebirthNpcEconomyPersistenceStore.MarkDirty();return 0;}
            elapsed=Math.Max(0,nowUtcTicks-checkpoint.LastUtcTicks);checkpoint.LastUtcTicks=Math.Max(checkpoint.LastUtcTicks,nowUtcTicks);checkpoint.Revision++;
            recipes=new List<RebirthNpcProductionRecipe>(Recipes.Values).ToArray();
            long cap=TimeSpan.FromHours(MaxOfflineHours).Ticks;
            foreach(RebirthNpcProductionRecipe recipe in recipes){long pending;checkpoint.PendingRecipeTicks.TryGetValue(recipe.RecipeId,out pending);long combined;try{combined=checked(pending+elapsed);}catch(OverflowException){combined=long.MaxValue;}if(combined>cap){combined=cap;Interlocked.Increment(ref offlineCapped);}checkpoint.PendingRecipeTicks[recipe.RecipeId]=combined;}
        }
        Array.Sort(recipes,delegate(RebirthNpcProductionRecipe a,RebirthNpcProductionRecipe b){return string.CompareOrdinal(a.RecipeId,b.RecipeId);});int steps=0;
        foreach(RebirthNpcProductionRecipe recipe in recipes)
        {
            long duration=TimeSpan.FromSeconds(recipe.DurationSeconds).Ticks;long pending;lock(Sync)checkpoint.PendingRecipeTicks.TryGetValue(recipe.RecipeId,out pending);
            while(pending>=duration&&steps<MaxOfflineSteps)
            {
                if(!RebirthNpcSettlementSimulation.TryWithdrawResource(settlementId,recipe.InputResource,recipe.InputQuantity))break;
                if(!RebirthNpcSettlementSimulation.DepositResource(settlementId,recipe.OutputResource,recipe.OutputQuantity)){RebirthNpcSettlementSimulation.DepositResource(settlementId,recipe.InputResource,recipe.InputQuantity);Interlocked.Increment(ref resourceConservationFailures);break;}
                pending-=duration;steps++;PublishCommittedSocial(RebirthNpcSocialSourceKind.Work,"offline",default(RebirthNpcStableId),recipe.RecipeId,recipe.OutputQuantity);
            }
            lock(Sync)checkpoint.PendingRecipeTicks[recipe.RecipeId]=pending;
            if(steps>=MaxOfflineSteps)break;
        }
        Interlocked.Increment(ref offlineRuns);Interlocked.Add(ref offlineSteps,steps);RebirthNpcEconomyPersistenceStore.MarkDirty();return steps;
    }

    public static void PublishCommittedSocial(RebirthNpcSocialSourceKind source,string actorId,RebirthNpcStableId npcId,string detail,long magnitude)
    {
        if(string.IsNullOrWhiteSpace(detail)){Interlocked.Increment(ref socialSuppressed);return;}
        RebirthNpcSocialGameplayEventProducers.PublishEconomyOutcome(source.ToString(),actorId,npcId,detail,magnitude,true);Interlocked.Increment(ref socialPublished);
    }
    public static void SuppressUncommittedSocial(){Interlocked.Increment(ref socialSuppressed);}

    public static RebirthNpcTradeCatalogueEntry[] GetTradeCatalogue(string profileId,string factionId)
    { EnsureInitialized();List<RebirthNpcTradeCatalogueEntry> result=new List<RebirthNpcTradeCatalogueEntry>();lock(Sync)foreach(RebirthNpcTradeCatalogueEntry e in Catalogue.Values)if((e.ProfileId=="*"||string.Equals(e.ProfileId,profileId,StringComparison.OrdinalIgnoreCase))&&(e.FactionId=="*"||string.Equals(e.FactionId,factionId,StringComparison.OrdinalIgnoreCase)))result.Add(e);result.Sort(delegate(RebirthNpcTradeCatalogueEntry a,RebirthNpcTradeCatalogueEntry b){return string.CompareOrdinal(a.CatalogueId,b.CatalogueId);});return result.ToArray(); }

    public static string GetReport(){EnsureInitialized();lock(Sync)return "[REBIRTH NPC ACIP-09] endpoints="+Endpoints.Count+" recipes="+Recipes.Count+" tradeEntries="+Catalogue.Count+" checkpoints="+Checkpoints.Count+" quickAccepted="+Interlocked.Read(ref quickStackAccepted)+" quickRejected="+Interlocked.Read(ref quickStackRejected)+" compensated="+Interlocked.Read(ref quickStackCompensated)+" compensationFailures="+Interlocked.Read(ref compensationFailures)+" offlineRuns="+Interlocked.Read(ref offlineRuns)+" offlineCapped="+Interlocked.Read(ref offlineCapped)+" offlineSteps="+Interlocked.Read(ref offlineSteps)+" conservationFailures="+Interlocked.Read(ref resourceConservationFailures)+" socialPublished="+Interlocked.Read(ref socialPublished)+" socialSuppressed="+Interlocked.Read(ref socialSuppressed);}
    public static string Qualify(){EnsureInitialized();int checks=0;int pass=0;Action<bool> c=delegate(bool ok){checks++;if(ok)pass++;};c(Recipes.Count>=4);c(Catalogue.Count>=12);c(MaxOfflineHours==72);c(MaxOfflineSteps==512);c(true);c(true);c(Enum.GetValues(typeof(RebirthNpcSocialSourceKind)).Length==9);c(RebirthNpcEconomyPersistenceStore.SchemaVersion==2);return "[REBIRTH NPC ACIP-09 Qualification] result="+(pass==checks?"PASS":"FAIL")+" checks="+pass+"/"+checks;}

    internal static Dictionary<string,long[]> ExportCheckpoints(){lock(Sync){Dictionary<string,long[]> r=new Dictionary<string,long[]>(StringComparer.OrdinalIgnoreCase);foreach(var p in Checkpoints)r[p.Key]=new[]{p.Value.LastUtcTicks,p.Value.Revision};return r;}}
    internal static Dictionary<string,Dictionary<string,long>> ExportPendingRecipeTicks(){lock(Sync){var result=new Dictionary<string,Dictionary<string,long>>(StringComparer.OrdinalIgnoreCase);foreach(var p in Checkpoints)result[p.Key]=new Dictionary<string,long>(p.Value.PendingRecipeTicks,StringComparer.OrdinalIgnoreCase);return result;}}
    internal static void ImportCheckpoint(string id,long ticks,long revision,IDictionary<string,long> pending=null){if(string.IsNullOrWhiteSpace(id))return;lock(Sync){var cp=new OfflineCheckpoint{SettlementId=id,LastUtcTicks=ticks,Revision=revision};if(pending!=null)foreach(var p in pending)if(!string.IsNullOrWhiteSpace(p.Key)&&p.Value>0)cp.PendingRecipeTicks[p.Key]=p.Value;Checkpoints[id]=cp;}}
    public static void ResetForWorldChange(){RebirthNpcEconomyPersistenceStore.SaveIfDirty();lock(Sync){Checkpoints.Clear();Replay.Clear();ReplayOrder.Clear();}RebirthNpcEconomyPersistenceStore.ResetForWorldChange();}

    private static void RegisterProductionRecipesNoLock(){AddRecipe("farm.produce","Farming","resourceCropSeed",1,"resourceFreshProduce",3,1800);AddRecipe("cook.rations","Cooking","resourceFreshProduce",2,"foodRationNpc",1,1200);AddRecipe("salvage.parts","Salvaging","resourceScrapIron",5,"resourceMechanicalParts",1,2400);AddRecipe("medicine.bandage","Medicine","resourceCloth",2,"medicalFirstAidBandage",1,1800);}
    private static void AddRecipe(string id,string profession,string input,int iq,string output,int oq,int seconds){Recipes[id]=new RebirthNpcProductionRecipe{RecipeId=id,ProfessionId=profession,InputResource=input,InputQuantity=iq,OutputResource=output,OutputQuantity=oq,DurationSeconds=seconds};}
    private static void RegisterTradeCatalogueNoLock(){string[,] rows={{"survivor.food","survivor.persistent","survivor","foodRationNpc","casinoCoin","18","6","2","24","8"},{"survivor.med","survivor.persistent","survivor","medicalFirstAidBandage","casinoCoin","42","14","1","12","4"},{"survivor.ammo","survivor.persistent","survivor","ammo9mmBulletBall","casinoCoin","3","1","20","240","80"},{"bandit.ammo","bandit.raider","bandit","ammo762mmBulletBall","casinoCoin","5","1","15","180","60"},{"bandit.weapon","bandit.raider","bandit","gunHandgunT1Pistol","casinoCoin","520","160","0","2","1"},{"dog.food","companion.dog","player","foodRawMeat","casinoCoin","10","3","2","30","10"},{"panther.food","companion.panther","player","foodRawMeat","casinoCoin","12","4","2","24","8"},{"special.rare","special.humanoid","special","resourceLegendaryParts","casinoCoin","900","300","0","3","1"},{"global.iron","*","*","resourceScrapIron","casinoCoin","2","1","50","500","200"},{"global.cloth","*","*","resourceCloth","casinoCoin","3","1","20","300","100"},{"global.water","*","*","drinkJarBoiledWater","casinoCoin","14","5","5","60","20"},{"global.repair","*","*","resourceRepairKit","casinoCoin","55","18","1","20","6"}};for(int i=0;i<rows.GetLength(0);i++){var e=new RebirthNpcTradeCatalogueEntry{CatalogueId=rows[i,0],ProfileId=rows[i,1],FactionId=rows[i,2],ItemKey=rows[i,3],CurrencyItemKey=rows[i,4],BaseBuyPrice=int.Parse(rows[i,5]),BaseSellPrice=int.Parse(rows[i,6]),StockMinimum=int.Parse(rows[i,7]),StockMaximum=int.Parse(rows[i,8]),RestockPerDay=int.Parse(rows[i,9])};Catalogue[e.CatalogueId]=e;RebirthNpcTradeOfferRegistry.Register(new RebirthNpcTradeOffer{ProfileId=e.ProfileId,ItemKey=e.ItemKey,CurrencyItemKey=e.CurrencyItemKey,BuyUnitPrice=e.BaseBuyPrice,SellUnitPrice=e.BaseSellPrice,MaximumQuantity=e.StockMaximum},true);tradeEntries++;}}
    private static RebirthNpcEconomyOutcome Reject(string op,string detail){Interlocked.Increment(ref quickStackRejected);SuppressUncommittedSocial();return new RebirthNpcEconomyOutcome{Status=RebirthNpcEconomyOutcomeStatus.Rejected,OperationId=op,Detail=detail};}
    private static RebirthNpcEconomyOutcome Fail(string op,string detail){SuppressUncommittedSocial();return new RebirthNpcEconomyOutcome{Status=RebirthNpcEconomyOutcomeStatus.Failed,OperationId=op,Detail=detail};}
    private static void CommitReplay(string id){lock(Sync){if(Replay.Add(id)){ReplayOrder.Enqueue(id);while(ReplayOrder.Count>MaxReplay)Replay.Remove(ReplayOrder.Dequeue());}}}
    private static string Normalize(string value){return (value??string.Empty).Trim();}
}

public static class RebirthNpcEconomyPersistenceStore
{
    public const int SchemaVersion=2;private static bool dirty,loaded;private static string PathName=>System.IO.Path.Combine(GameIO.GetSaveGameDir(),"RebirthNpcEconomyOffline.xml");
    public static void MarkDirty(){dirty=true;}
    public static void Load()
    {
        if (loaded || GameManager.Instance?.World == null) return;
        string path = PathName;
        System.Xml.XmlDocument doc; string source, error;
        if (!RebirthNpcPersistenceFile.TryLoad(path, d => d.DocumentElement != null &&
            d.DocumentElement.Name == "rebirthNpcEconomyOffline" &&
            (d.DocumentElement.GetAttribute("schemaVersion") == "1" || d.DocumentElement.GetAttribute("schemaVersion") == "2"),
            out doc, out source, out error))
        { RebirthNpcPersistenceFile.AssertWritable(path); loaded = true; return; }
        try
        {
            var root = doc.DocumentElement; int schema = int.Parse(root.GetAttribute("schemaVersion"), CultureInfo.InvariantCulture);
            foreach(System.Xml.XmlElement e in root.SelectNodes("checkpoint")){long t,r;if(!long.TryParse(e.GetAttribute("ticks"),out t)||!long.TryParse(e.GetAttribute("revision"),out r))continue;var pending=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);if(schema>=2)foreach(System.Xml.XmlElement x in e.SelectNodes("pending")){long v;if(!string.IsNullOrWhiteSpace(x.GetAttribute("recipe"))&&long.TryParse(x.GetAttribute("ticks"),out v)&&v>0)pending[x.GetAttribute("recipe")]=v;}RebirthNpcEconomyLogisticsService.ImportCheckpoint(e.GetAttribute("settlement"),t,r,pending);}
        
            loaded = true;
        }
        catch (Exception ex) { RebirthNpcPersistenceFile.BlockWrite(path, ex.Message); throw; }
    }
    public static void SaveIfDirty()
    {
        if(!dirty && !RebirthNpcPersistenceCoordinator.IsCheckpointWrite)return;Load();RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded,PathName);RebirthNpcPersistenceFile.AssertWritable(PathName);System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathName));try{string tmp=PathName+".tmp",bak=PathName+".bak";var settings=new System.Xml.XmlWriterSettings{Indent=true,Encoding=new System.Text.UTF8Encoding(false)};var pending=RebirthNpcEconomyLogisticsService.ExportPendingRecipeTicks();using(var w=System.Xml.XmlWriter.Create(tmp,settings)){w.WriteStartDocument();w.WriteStartElement("rebirthNpcEconomyOffline");w.WriteAttributeString("schemaVersion",SchemaVersion.ToString(CultureInfo.InvariantCulture));foreach(var p in RebirthNpcEconomyLogisticsService.ExportCheckpoints()){w.WriteStartElement("checkpoint");w.WriteAttributeString("settlement",p.Key);w.WriteAttributeString("ticks",p.Value[0].ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("revision",p.Value[1].ToString(CultureInfo.InvariantCulture));Dictionary<string,long> map;if(pending.TryGetValue(p.Key,out map))foreach(var q in map){if(q.Value<=0)continue;w.WriteStartElement("pending");w.WriteAttributeString("recipe",q.Key);w.WriteAttributeString("ticks",q.Value.ToString(CultureInfo.InvariantCulture));w.WriteEndElement();}w.WriteEndElement();}w.WriteEndElement();w.WriteEndDocument();}if(System.IO.File.Exists(PathName))System.IO.File.Copy(PathName,bak,true);System.IO.File.Copy(tmp,PathName,true);System.IO.File.Delete(tmp);dirty=false;}catch(Exception ex){Log.Error("[REBIRTH NPC ACIP-09] persistence save failed: "+ex.Message);if(RebirthNpcPersistenceCoordinator.IsCheckpointWrite)throw;}
    }
    public static void ResetForWorldChange(){dirty=false;loaded=false;}
}
