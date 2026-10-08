using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;

#nullable disable

public sealed class RebirthSalvageProfileMatch
{
    public string Mode=string.Empty;
    public string Value=string.Empty;
    public bool Matches(string blockName)
    {
        string n=(blockName??string.Empty).ToLowerInvariant(),v=(Value??string.Empty).ToLowerInvariant();
        if(v.Length==0)return false;
        if(string.Equals(Mode,"exact",StringComparison.OrdinalIgnoreCase))return n==v;
        if(string.Equals(Mode,"prefix",StringComparison.OrdinalIgnoreCase))return n.StartsWith(v,StringComparison.Ordinal);
        return n.Contains(v);
    }
}

public sealed class RebirthSalvageValuableEntry
{
    public string Item=string.Empty;
    public float Weight=1f;
}

public sealed class RebirthSalvageProfileDefinition
{
    public string Id=string.Empty;
    public int Priority;
    public readonly List<RebirthSalvageProfileMatch> Matches=new List<RebirthSalvageProfileMatch>();
    public readonly HashSet<string> Ordinary=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly List<RebirthSalvageValuableEntry> Valuable=new List<RebirthSalvageValuableEntry>();
    public bool MatchesBlock(string blockName){for(int i=0;i<Matches.Count;i++)if(Matches[i].Matches(blockName))return true;return false;}
    public RebirthSalvageValuableEntry FindValuable(string item){for(int i=0;i<Valuable.Count;i++)if(string.Equals(Valuable[i].Item,item,StringComparison.OrdinalIgnoreCase))return Valuable[i];return null;}
}

public sealed class RebirthAuthoredSalvageDrop
{
    public string ItemName=string.Empty;
    public int MaxCount;
    public float Probability;
}

/// <summary>
/// PC027 / Chunk N. Data-driven Scavenger salvage specialization. Native salvage remains the
/// source of truth: this service observes an item that the normal collectHarvestedItem path
/// actually placed into the player's inventory, then server-validates the block, tool, profile,
/// authored native salvage drop and damage progression before granting a bounded bonus.
/// Unmatched blocks always fall back to native salvage with no bonus.
/// </summary>
public static class RebirthScavengerSalvageProfileService
{
    public const string BonusId="background_bonus.nothing_is_junk";
    private const string ConfigFile="salvage_profiles.xml";
    private static readonly object Gate=new object();
    private static readonly List<RebirthSalvageProfileDefinition> Profiles=new List<RebirthSalvageProfileDefinition>();
    private static readonly Dictionary<string,int> OrdinaryReplayDamage=new Dictionary<string,int>(StringComparer.Ordinal);
    private static readonly Dictionary<string,int> ValuableReplayDamage=new Dictionary<string,int>(StringComparer.Ordinal);
    private static readonly Dictionary<string,int> TotalExtraAtDamage=new Dictionary<string,int>(StringComparer.Ordinal);
    private static bool installed,harvestPatched,loaded;
    private static string loadStatus="not_loaded";
    private static string sourcePath=string.Empty;
    private static int maxOrdinaryExtraPerOutcome=12,maxValuableExtraPerDamage=1,maxTotalExtraPerDamage=18,maxSingleBaseCount=128;
    private static float maxValuableBonusChance=0.35f;

    public struct HarvestState
    {
        public EntityPlayer Player;
        public Vector3i Position;
        public string BlockName;
        public string ProfileId;
        public uint BlockRawData;
        public int DamageBefore;
        public int ItemType;
        public string ItemName;
        public int CountBefore;
        public bool Eligible;
    }

    private sealed class ValuableCandidate
    {
        public string ItemName;
        public float Chance;
        public float Weight;
        public ValuableCandidate(string item,float chance,float weight){ItemName=item;Chance=chance;Weight=weight;}
    }

    public static string Install(Harmony harmony)
    {
        if(installed)return "[REBIRTH Scavenger Salvage Profiles] already installed "+loadStatus;
        installed=true;
        LoadProfiles();
        if(harmony!=null)
        {
            MethodInfo harvest=AccessTools.Method(typeof(GameUtils),"collectHarvestedItem");
            if(harvest!=null)
            {
                harmony.Patch(harvest,prefix:new HarmonyMethod(typeof(RebirthScavengerSalvageProfileService),nameof(HarvestPrefix)),postfix:new HarmonyMethod(typeof(RebirthScavengerSalvageProfileService),nameof(HarvestPostfix)));
                harvestPatched=true;
            }
        }
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        return "[REBIRTH Scavenger Salvage Profiles] installed profiles="+Profiles.Count+" harvest="+harvestPatched+" "+loadStatus;
    }

    public static void HarvestPrefix(ItemActionData _actionData,ItemValue _iv,out HarvestState __state)
    {
        __state=new HarvestState();
        if(!loaded||_actionData==null||_actionData.invData==null||_actionData.attackDetails==null||_iv==null||_iv.ItemClass==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        EntityPlayer player=_actionData.invData.holdingEntity as EntityPlayer;
        if(player==null||player.world==null)return;
        if(!string.Equals(RebirthProgressionRuntimeConfig.ClassifyHarvestTool(_actionData.invData.itemValue),"skill.salvage",StringComparison.OrdinalIgnoreCase))return;
        BlockValue damagedValue=_actionData.attackDetails.blockBeingDamaged;
        Block block=damagedValue.Block;
        string blockName=block!=null?(block.GetBlockName()??string.Empty):string.Empty;
        RebirthSalvageProfileDefinition profile=MatchProfile(blockName);
        if(profile==null)return;
        string itemName=_iv.ItemClass.GetItemName()??string.Empty;
        if(!profile.Ordinary.Contains(itemName)&&profile.FindValuable(itemName)==null)return;
        if(!IsAuthoredNativeSalvageDrop(blockName,itemName,null))return;
        __state.Player=player;
        __state.Position=_actionData.attackDetails.raycastHitPosition;
        __state.BlockName=blockName;
        __state.ProfileId=profile.Id;
        __state.BlockRawData=damagedValue.rawData;
        __state.DamageBefore=damagedValue.damage;
        __state.ItemType=_iv.type;
        __state.ItemName=itemName;
        __state.CountBefore=CountPlayerItem(player,_iv.type);
        __state.Eligible=true;
    }

    public static void HarvestPostfix(HarvestState __state)
    {
        if(!__state.Eligible||__state.Player==null)return;
        int after=CountPlayerItem(__state.Player,__state.ItemType);
        int delta=after-__state.CountBefore;
        if(delta<=0)return; // fail closed: no native inventory outcome, no bonus request
        if(__state.Player.world!=null&&__state.Player.world.IsRemote())
        {
            ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(c!=null)c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthScavengerSalvageRequest>().Setup(__state.Player.entityId,__state.Position,__state.BlockName,__state.ProfileId,__state.BlockRawData,__state.DamageBefore,__state.ItemType,Math.Min(delta,maxSingleBaseCount)));
        }
        else ProcessSalvageRequest(__state.Player,__state.Position,__state.BlockName,__state.ProfileId,__state.BlockRawData,__state.DamageBefore,__state.ItemType,Math.Min(delta,maxSingleBaseCount));
    }

    public static void ProcessSalvageRequest(EntityPlayer player,Vector3i pos,string requestedBlockName,string requestedProfileId,uint expectedRawData,int damageBefore,int itemType,int baseCount)
    {
        if(!loaded||player==null||player.world==null||player.world.IsRemote()||baseCount<=0||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!RebirthBackgroundBonusService.HasBonus(player,BonusId))return;
        RebirthSalvageProfileDefinition profile=MatchProfile(requestedBlockName);
        if(profile==null||!string.Equals(profile.Id,requestedProfileId,StringComparison.OrdinalIgnoreCase))return;
        ItemClass itemClass=ItemClass.GetForId(itemType);
        if(itemClass==null)return;
        string baseItemName=itemClass.GetItemName()??string.Empty;
        RebirthAuthoredSalvageDrop baseDrop;
        if(!IsAuthoredNativeSalvageDrop(requestedBlockName,baseItemName,out baseDrop)||baseDrop==null)return;
        baseCount=Math.Min(Math.Min(baseCount,maxSingleBaseCount),Math.Max(0,baseDrop.MaxCount));
        if(baseCount<=0)return;
        float range=Constants.cDigAndBuildDistance+3f;
        if((player.position-pos.ToVector3()).sqrMagnitude>range*range)return;
        ItemValue held=player.inventory!=null?player.inventory.holdingItemItemValue:null;
        if(!string.Equals(RebirthProgressionRuntimeConfig.ClassifyHarvestTool(held),"skill.salvage",StringComparison.OrdinalIgnoreCase))return;
        BlockValue current=player.world.GetBlock(pos);
        string currentName=current.Block!=null?(current.Block.GetBlockName()??string.Empty):string.Empty;
        if(current.rawData!=expectedRawData||current.damage<=damageBefore)return;
        if(!string.Equals(currentName,requestedBlockName,StringComparison.OrdinalIgnoreCase))return; // fail closed on replacement/final destruction
        int damage=current.damage;
        int totalExtra=GetTotalExtra(player.entityId,pos,damage);
        if(totalExtra>=maxTotalExtraPerDamage)return;

        int ordinaryExtra=0;
        if(profile.Ordinary.Contains(baseItemName))
        {
            string replay=player.entityId+"|"+pos.x+","+pos.y+","+pos.z+"|"+itemType;
            if(ClaimDamage(OrdinaryReplayDamage,replay,damage))
            {
                float multiplier=GetBonusTuning("ordinary_output_multiplier",1.35f);
                ordinaryExtra=StochasticRound(player.world,Math.Max(0f,baseCount*(Mathf.Max(1f,multiplier)-1f)));
                ordinaryExtra=Math.Min(ordinaryExtra,maxOrdinaryExtraPerOutcome);
                ordinaryExtra=Math.Min(ordinaryExtra,Math.Max(0,maxTotalExtraPerDamage-totalExtra));
                if(ordinaryExtra>0)
                {
                    GrantExtra(player.entityId,itemType,ordinaryExtra);
                    AddTotalExtra(player.entityId,pos,damage,ordinaryExtra);
                    totalExtra+=ordinaryExtra;
                }
            }
        }

        if(totalExtra<maxTotalExtraPerDamage&&maxValuableExtraPerDamage>0)
        {
            string valuableReplay=player.entityId+"|"+pos.x+","+pos.y+","+pos.z+"|"+profile.Id;
            if(ClaimDamage(ValuableReplayDamage,valuableReplay,damage))
            {
                ValuableCandidate chosen=RollValuableCandidate(player.world,requestedBlockName,profile);
                if(chosen!=null)
                {
                    ItemValue value=ItemClass.GetItem(chosen.ItemName,false);
                    if(value!=null&&!value.IsEmpty()&&value.ItemClass!=null)
                    {
                        int count=Math.Min(maxValuableExtraPerDamage,Math.Max(0,maxTotalExtraPerDamage-totalExtra));
                        if(count>0){GrantExtra(player.entityId,value.type,count);AddTotalExtra(player.entityId,pos,damage,count);}
                    }
                }
            }
        }
    }

    private static ValuableCandidate RollValuableCandidate(World world,string blockName,RebirthSalvageProfileDefinition profile)
    {
        if(world==null||profile==null)return null;
        List<RebirthAuthoredSalvageDrop> authored=GetAuthoredSalvageDrops(blockName);
        if(authored.Count==0)return null;
        float multiplier=GetBonusTuning("valuable_chance_multiplier",1.75f);
        List<ValuableCandidate> candidates=new List<ValuableCandidate>();
        for(int i=0;i<authored.Count;i++)
        {
            RebirthSalvageValuableEntry e=profile.FindValuable(authored[i].ItemName);
            if(e==null)continue;
            float chance=Mathf.Clamp(authored[i].Probability*Mathf.Max(0f,multiplier-1f),0f,maxValuableBonusChance);
            if(chance>0f)candidates.Add(new ValuableCandidate(authored[i].ItemName,chance,Math.Max(0.01f,e.Weight)));
        }
        if(candidates.Count==0)return null;
        float total=0f;for(int i=0;i<candidates.Count;i++)total+=candidates[i].Chance*candidates[i].Weight;
        float aggregate=Mathf.Clamp(total,0f,maxValuableBonusChance);
        if(world.GetGameRandom().RandomFloat>=aggregate)return null;
        float roll=world.GetGameRandom().RandomFloat*total,acc=0f;
        for(int i=0;i<candidates.Count;i++){acc+=candidates[i].Chance*candidates[i].Weight;if(roll<=acc)return candidates[i];}
        return candidates[candidates.Count-1];
    }

    public static void GrantExtra(int playerId,int itemType,int count)
    {
        if(count<=0||GameManager.Instance==null||GameManager.Instance.World==null)return;
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        EntityPlayer p=GameManager.Instance.World.GetEntity(playerId) as EntityPlayer;
        if(c!=null&&c.IsServer&&!(p is EntityPlayerLocal))
        {
            c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthScavengerSalvageGrant>().Setup(playerId,itemType,count),_attachedToEntityId:playerId);
            return;
        }
        EntityPlayerLocal local=GameManager.Instance.World.GetPrimaryPlayer();
        if(local==null||local.entityId!=playerId)return;
        ItemValue value=new ItemValue(itemType);if(value==null||value.ItemClass==null)return;
        ItemStack stack=new ItemStack(value,count);LocalPlayerUI ui=LocalPlayerUI.GetUIForPlayer(local);
        if(ui!=null&&ui.xui!=null&&ui.xui.PlayerInventory.AddItem(stack))return;
        if(stack.count>0)local.world.gameManager.ItemDropServer(stack,local.GetDropPosition(),Vector3.zero,local.entityId,60f,false);
    }

    public static RebirthSalvageProfileDefinition MatchProfile(string blockName)
    {
        if(string.IsNullOrWhiteSpace(blockName))return null;
        for(int i=0;i<Profiles.Count;i++)if(Profiles[i].MatchesBlock(blockName))return Profiles[i];
        return null;
    }

    public static List<RebirthAuthoredSalvageDrop> GetAuthoredSalvageDrops(string blockName)
    {
        List<RebirthAuthoredSalvageDrop> result=new List<RebirthAuthoredSalvageDrop>();
        if(string.IsNullOrWhiteSpace(blockName))return result;
        BlockValue bv;try{bv=Block.GetBlockValue(blockName);}catch{return result;}
        Block block=bv.Block;if(block==null)return result;
        AddAuthoredDrops(block,EnumDropEvent.Harvest,result);
        AddAuthoredDrops(block,EnumDropEvent.Destroy,result);
        return result;
    }

    private static void AddAuthoredDrops(Block block,EnumDropEvent evt,List<RebirthAuthoredSalvageDrop> result)
    {
        List<Block.SItemDropProb> drops;
        if(block==null||block.itemsToDrop==null||!block.itemsToDrop.TryGetValue(evt,out drops)||drops==null)return;
        for(int i=0;i<drops.Count;i++)
        {
            Block.SItemDropProb d=drops[i];
            bool salvage=(d.tag??string.Empty).IndexOf("salvageHarvest",StringComparison.OrdinalIgnoreCase)>=0||string.Equals(d.toolCategory,"Disassemble",StringComparison.OrdinalIgnoreCase);
            if(!salvage||string.IsNullOrWhiteSpace(d.name)||d.name=="*"||d.name=="[recipe]")continue;
            RebirthAuthoredSalvageDrop existing=result.Find(x=>string.Equals(x.ItemName,d.name,StringComparison.OrdinalIgnoreCase));
            if(existing==null)result.Add(new RebirthAuthoredSalvageDrop{ItemName=d.name,MaxCount=Math.Max(0,d.maxCount),Probability=Mathf.Clamp01(d.prob)});
            else{existing.MaxCount=Math.Min(maxSingleBaseCount,existing.MaxCount+Math.Max(0,d.maxCount));existing.Probability=Mathf.Max(existing.Probability,Mathf.Clamp01(d.prob));}
        }
    }

    private static bool IsAuthoredNativeSalvageDrop(string blockName,string itemName,out RebirthAuthoredSalvageDrop found)
    {
        found=null;List<RebirthAuthoredSalvageDrop> drops=GetAuthoredSalvageDrops(blockName);
        for(int i=0;i<drops.Count;i++)if(string.Equals(drops[i].ItemName,itemName,StringComparison.OrdinalIgnoreCase)){found=drops[i];return true;}
        return false;
    }
    private static bool IsAuthoredNativeSalvageDrop(string blockName,string itemName,object unused){RebirthAuthoredSalvageDrop d;return IsAuthoredNativeSalvageDrop(blockName,itemName,out d);}

    private static void LoadProfiles()
    {
        Profiles.Clear();loaded=false;sourcePath=string.Empty;
        try
        {
            sourcePath=Path.Combine(RebirthSurvivorDefinitionLoader.ResolveConfigRoot(),ConfigFile);
            if(!File.Exists(sourcePath)){loadStatus="missing="+sourcePath;return;}
            XDocument doc=XDocument.Load(sourcePath,LoadOptions.None);XElement root=doc.Root;
            if(root==null||!string.Equals(root.Name.LocalName,"rebirth_salvage_profiles",StringComparison.OrdinalIgnoreCase)){loadStatus="invalid_root";return;}
            maxOrdinaryExtraPerOutcome=ReadInt(root,"max_ordinary_extra_per_outcome",12,1,128);
            maxValuableExtraPerDamage=ReadInt(root,"max_valuable_extra_per_damage",1,0,8);
            maxTotalExtraPerDamage=ReadInt(root,"max_total_extra_per_damage",18,1,256);
            maxSingleBaseCount=ReadInt(root,"max_single_base_count",128,1,1024);
            maxValuableBonusChance=ReadFloat(root,"max_valuable_bonus_chance",0.35f,0f,1f);
            foreach(XElement e in root.Elements("profile"))
            {
                RebirthSalvageProfileDefinition p=new RebirthSalvageProfileDefinition{Id=((string)e.Attribute("id")??string.Empty).Trim(),Priority=ReadInt(e,"priority",0,-1000,1000)};
                if(p.Id.Length==0)continue;
                foreach(XElement m in e.Elements("match")){string mode=((string)m.Attribute("mode")??"contains").Trim();string value=((string)m.Attribute("value")??string.Empty).Trim();if(value.Length>0)p.Matches.Add(new RebirthSalvageProfileMatch{Mode=mode,Value=value});}
                foreach(XElement o in e.Elements("ordinary")){string item=((string)o.Attribute("item")??string.Empty).Trim();if(item.Length>0&&!IsCurrency(item))p.Ordinary.Add(item);}
                foreach(XElement v in e.Elements("valuable")){string item=((string)v.Attribute("item")??string.Empty).Trim();if(item.Length>0&&!IsCurrency(item))p.Valuable.Add(new RebirthSalvageValuableEntry{Item=item,Weight=ReadFloat(v,"weight",1f,0.01f,100f)});}
                if(p.Matches.Count>0&&(p.Ordinary.Count>0||p.Valuable.Count>0))Profiles.Add(p);
            }
            Profiles.Sort((a,b)=>{int c=b.Priority.CompareTo(a.Priority);return c!=0?c:string.Compare(a.Id,b.Id,StringComparison.OrdinalIgnoreCase);});
            loaded=Profiles.Count>0;loadStatus=loaded?"loaded="+Profiles.Count+" source="+sourcePath:"no_profiles";
        }
        catch(Exception ex){loadStatus="load_failed="+ex.GetType().Name+":"+ex.Message;Log.Warning("[REBIRTH Scavenger Salvage Profiles] "+loadStatus);}
    }

    private static bool IsCurrency(string item){string n=(item??string.Empty).ToLowerInvariant();return n.Contains("casino")||n.Contains("coin")||n.Contains("cash")||n.Contains("money")||n.Contains("duke");}
    private static float GetBonusTuning(string key,float fallback){RebirthBackgroundBonusDefinition d;RebirthBackgroundBonusTuningValue v;float f;return RebirthBackgroundBonusRegistry.TryGet(BonusId,out d)&&d!=null&&d.TryGetTuning(key,out v)&&v!=null&&float.TryParse(v.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out f)?f:fallback;}
    private static bool ClaimDamage(Dictionary<string,int> map,string key,int damage){lock(Gate){int previous;if(map.TryGetValue(key,out previous)&&damage<=previous)return false;map[key]=damage;return true;}}
    private static string TotalKey(int playerId,Vector3i pos,int damage){return playerId+"|"+pos.x+","+pos.y+","+pos.z+"|"+damage;}
    private static int GetTotalExtra(int playerId,Vector3i pos,int damage){lock(Gate){int v;return TotalExtraAtDamage.TryGetValue(TotalKey(playerId,pos,damage),out v)?v:0;}}
    private static void AddTotalExtra(int playerId,Vector3i pos,int damage,int count){lock(Gate){string k=TotalKey(playerId,pos,damage);int v;TotalExtraAtDamage.TryGetValue(k,out v);TotalExtraAtDamage[k]=Math.Min(maxTotalExtraPerDamage,v+Math.Max(0,count));if(TotalExtraAtDamage.Count>4096)TotalExtraAtDamage.Clear();}}
    private static int StochasticRound(World world,float raw){int whole=Mathf.FloorToInt(Math.Max(0f,raw));float f=raw-whole;if(f>0f&&world!=null&&world.GetGameRandom().RandomFloat<f)whole++;return whole;}
    private static int CountPlayerItem(EntityPlayer player,int type){int total=0;CountSlots(player!=null&&player.inventory!=null?player.inventory.ItemGrid.items:null,type,ref total);CountSlots(player!=null&&player.bag!=null?player.bag.ItemGrid.items:null,type,ref total);return total;}
    private static void CountSlots(ItemStack[] slots,int type,ref int total){if(slots==null)return;for(int i=0;i<slots.Length;i++)if(slots[i]!=null&&slots[i].itemValue!=null&&slots[i].itemValue.type==type)total+=Math.Max(0,slots[i].count);}
    private static int ReadInt(XElement e,string a,int fallback,int min,int max){int v;return int.TryParse((string)e.Attribute(a),NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?Math.Max(min,Math.Min(max,v)):fallback;}
    private static float ReadFloat(XElement e,string a,float fallback,float min,float max){float v;return float.TryParse((string)e.Attribute(a),NumberStyles.Float,CultureInfo.InvariantCulture,out v)?Mathf.Clamp(v,min,max):fallback;}

    public static string BuildDebugReport(EntityPlayer player,Vector3i? pos)
    {
        StringBuilder b=new StringBuilder();
        b.AppendLine("[REBIRTH Chunk N / Scavenger Salvage Profiles]");
        b.AppendLine("status="+(loaded?"IMPLEMENTED_PREBOOT":"NOT_READY")+" installed="+installed+" harvestPatched="+harvestPatched+" profiles="+Profiles.Count);
        b.AppendLine("source="+sourcePath);
        b.AppendLine("tuning ordinaryMultiplier="+GetBonusTuning("ordinary_output_multiplier",1.35f).ToString("0.###",CultureInfo.InvariantCulture)+" valuableChanceMultiplier="+GetBonusTuning("valuable_chance_multiplier",1.75f).ToString("0.###",CultureInfo.InvariantCulture)+" valuableChanceCap="+maxValuableBonusChance.ToString("0.###",CultureInfo.InvariantCulture));
        b.AppendLine("caps ordinaryPerOutcome="+maxOrdinaryExtraPerOutcome+" valuablePerDamage="+maxValuableExtraPerDamage+" totalPerDamage="+maxTotalExtraPerDamage+" baseCount="+maxSingleBaseCount);
        if(player!=null)b.AppendLine("player="+player.entityId+" ownsNothingIsJunk="+RebirthBackgroundBonusService.HasBonus(player,BonusId));
        if(pos.HasValue&&GameManager.Instance!=null&&GameManager.Instance.World!=null)
        {
            BlockValue bv=GameManager.Instance.World.GetBlock(pos.Value);string n=bv.Block!=null?(bv.Block.GetBlockName()??string.Empty):string.Empty;RebirthSalvageProfileDefinition p=MatchProfile(n);
            b.AppendLine("block="+pos.Value+" name="+n+" profile="+(p!=null?p.Id:"<none/native-fallback>")+" damage="+bv.damage);
            if(p!=null)
            {
                b.AppendLine("ordinary="+string.Join(",",p.Ordinary.OrderBy(x=>x).ToArray()));
                b.AppendLine("valuable="+string.Join(",",p.Valuable.Select(x=>x.Item+"@"+x.Weight.ToString("0.##",CultureInfo.InvariantCulture)).ToArray()));
                b.AppendLine("authoredSalvage="+string.Join(",",GetAuthoredSalvageDrops(n).Select(x=>x.ItemName+" max="+x.MaxCount+" p="+x.Probability.ToString("0.##",CultureInfo.InvariantCulture)).ToArray()));
            }
        }
        b.AppendLine("fullInventoryNativeOutcome=conservative: original ground-drop cannot be observed by inventory delta; no Scavenger bonus is fabricated in that case");
        return b.ToString().TrimEnd();
    }

    public static string RunVectors()
    {
        StringBuilder b=new StringBuilder("[REBIRTH Chunk N vectors]\n");
        b.AppendLine("profiles="+Profiles.Count+" noCatchall="+(!Profiles.Any(p=>p.Matches.Any(m=>string.Equals(m.Value,"*",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(m.Value)))));
        b.AppendLine("vehicle sedan="+(MatchProfile("cntSedan01")?.Id??"none")+" appliance fridge="+(MatchProfile("cntFridge01")?.Id??"none")+" plumbing sink="+(MatchProfile("utilitySink")?.Id??"none"));
        b.AppendLine("unknown custom="+(MatchProfile("myFutureCustomSalvageBlock")?.Id??"none/native-fallback"));
        float ordinary=Math.Max(0f,10f*(GetBonusTuning("ordinary_output_multiplier",1.35f)-1f));
        b.AppendLine("ordinary base10 rawExtra="+ordinary.ToString("0.###",CultureInfo.InvariantCulture)+" cap="+maxOrdinaryExtraPerOutcome);
        b.AppendLine("valuable p0.20 extraChance="+Mathf.Clamp(0.20f*Mathf.Max(0f,GetBonusTuning("valuable_chance_multiplier",1.75f)-1f),0f,maxValuableBonusChance).ToString("0.###",CultureInfo.InvariantCulture)+" cap="+maxValuableBonusChance.ToString("0.###",CultureInfo.InvariantCulture));
        b.AppendLine("selectionPolicy=profile roster AND authored native salvage drop; never player inventory/need inference");
        return b.ToString().TrimEnd();
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data){ClearRuntime();}
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data){ClearRuntime();}
    public static void ClearRuntime(){lock(Gate){OrdinaryReplayDamage.Clear();ValuableReplayDamage.Clear();TotalExtraAtDamage.Clear();}}
}
