using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Audio;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

public static class RebirthTraderVoices
{
    public sealed class Line
    {
        public string Id, Trader, Preset, Category, Text, Asset;
    }
    private static readonly Dictionary<string, List<Line>> Bank = new Dictionary<string, List<Line>>();
    private static readonly Dictionary<string, int> Last = new Dictionary<string, int>();
    private static readonly HashSet<string> Met = new HashSet<string>();
    private static readonly Queue<string> Events = new Queue<string>();
    private static readonly System.Random Random = new System.Random();
    public static bool Diagnostics;
    public static string Root;
    public static string LastLineId { get; private set; }
    public static string LastText { get; private set; }
    private static bool loaded;
    public static string[] RecentEvents => Events.ToArray();
    private static void Trace(string value)
    {
        if (!Diagnostics) return;
        if (Events.Count >= 64) Events.Dequeue();
        Events.Enqueue(value);
        Log.Out("[REBIRTH TraderVoices] " + value);
    }
    private static void Load()
    {
        if (loaded) return;
        var document = XDocument.Load(Path.Combine(Root, "Config", "_TraderVoices", "catalogue.xml"));
        foreach (var e in document.Root.Elements("line"))
        {
            string trader = (string)e.Attribute("trader");
            if (!RebirthTraderVoicePreferences.ValidTrader(trader)) continue; // Poppy TARGET is inactive.
            var line = new Line { Id=(string)e.Attribute("id"), Trader=trader, Preset=(string)e.Attribute("preset"),
                Category=(string)e.Attribute("category"), Text=e.Value, Asset=(string)e.Attribute("asset") };
            string key=trader+"/"+line.Preset+"/"+line.Category;
            if (!Bank.TryGetValue(key, out var list)) Bank[key]=list=new List<Line>();
            list.Add(line);
        }
        loaded=true;
    }
    public static string Trader(EntityNPC npc)
    {
        if (!(npc is EntityTrader) || npc.NPCInfo == null) return null;
        // Exact NPC identities exclude custom NPCs that merely borrow a vanilla voice set.
        string id=npc.NPCInfo.Id;
        switch(id?.ToLowerInvariant())
        {
            case "traderrekt": return "Rekt";
            case "traderhugh": return "Hugh";
            case "traderjen": return "Jen";
            case "traitorjoel": return "Joel";
            case "traderbob": return "Bob";
            default: return null;
        }
    }
    public static bool Prepare(string trader, string category, string original, out string replacement)
    {
        replacement=original;
        if (GameManager.IsDedicatedServer) { Trace("fallback=dedicated-no-local-playback"); return false; }
        if (LocalPlayerUI.GetUIForPrimaryPlayer()?.entityPlayer == null) { Trace("fallback=local-player-not-ready"); return false; }
        if (!RebirthTraderVoicePreferences.ValidTrader(trader)) { Trace("fallback=unknown-trader"); return false; }
        int selection=RebirthTraderVoicePreferences.Get(trader);
        if(selection==0) { Trace(trader+" fallback=original-preference"); return false; }
        string key=trader+"/"+RebirthTraderVoicePreferences.Presets[selection]+"/"+category;
        try
        {
            Load();
            if (!Bank.TryGetValue(key,out var lines) || lines.Count==0) { Trace(key+" fallback=unbound-category"); return false; }
            if (!Manager.audioData.TryGetValue(original,out var native) || native == null || native.audioClipMap == null || native.audioClipMap.Count==0)
            { Trace(key+" fallback=missing-native-sound original="+original); return false; }
            int index=Random.Next(lines.Count);
            if(lines.Count>1 && Last.TryGetValue(key,out var prior) && index==prior) index=(index+1+Random.Next(lines.Count-1))%lines.Count;
            Line line=lines[index];
            // Validate BEFORE replacing the original. Native audio, mixer, spatialization and subtitles handle playback.
            var clip=DataLoader.LoadAsset<AudioClip>(line.Asset);
            if(clip==null) { Trace(key+" id="+line.Id+" fallback=missing-clip"); return false; }
            var prefab=native.audioClipMap[0].audioSourceName;
            if(DataLoader.LoadAsset<GameObject>(prefab)==null) { Trace(key+" fallback=missing-native-source"); return false; }
            Manager.audioClipAssetCache[line.Asset]=clip;
            if (!Manager.audioData.ContainsKey(line.Id))
            {
                var data=new XmlData {soundGroupName=line.Id, maxVoices=native.maxVoices, maxRepeatRate=0,
                    localCrouchVolumeScale=native.localCrouchVolumeScale, runningVolumeScale=native.runningVolumeScale,
                    noiseData=native.noiseData, noiseScale=native.noiseScale,crouchNoiseScale=native.crouchNoiseScale,
                    channel=native.channel,priority=native.priority,maxVolume=native.maxVolume,
                    lowestPitch=1,highestPitch=1,vibratesController=false};
                data.audioClipMap.Add(new ClipSourceMap {clipName=line.Asset,audioSourceName=prefab,hasSubtitle=true,subtitleID=line.Id});
                Manager.audioData[line.Id]=data;
            }
            Manager.subtitleCache[line.Id]=new SubtitleData {name=line.Id, speakerLocId="npcTrader"+trader,contentLocId=line.Id};
            Last[key]=index; LastLineId=line.Id;LastText=line.Text;
            replacement=line.Id;
            Trace(key+" id="+line.Id+" fallback=none");
            return true;
        }
        catch(Exception ex) { Trace(key+" fallback="+ex.GetType().Name); return false; }
    }
    public static void Play(EntityNPC npc, string category)
    {
        string trader=Trader(npc);
        if(trader==null) return;
        string original="trader_"+trader.ToLowerInvariant()+"_greeting";
        if(Prepare(trader,category,original,out var replacement)) Manager.PlayInsidePlayerHead(replacement, -1, 0, false, true);
    }
    public static void PlayConfirmed(EntityNPC npc,string category,string originalEvent)
    {
        string trader=Trader(npc);
        if(RebirthTraderVoicePreferences.Get(trader)==0)return;
        string original="trader_"+trader.ToLowerInvariant()+"_"+originalEvent;
        Prepare(trader,category,original,out var replacement);
        Manager.PlayInsidePlayerHead(replacement,-1,0,false,true);
    }
    public static void Route(ref string sound)
    {
        if(string.IsNullOrEmpty(sound)||!sound.StartsWith("trader_",StringComparison.Ordinal))return;
        foreach(string trader in RebirthTraderVoicePreferences.Traders)
        {
            string prefix="trader_"+trader.ToLowerInvariant()+"_";
            if(!sound.StartsWith(prefix,StringComparison.Ordinal))continue;
            string evt=sound.Substring(prefix.Length), category=null;
            switch(evt)
            {
                case "firstgreet": case "greeting": case "greetmorn": case "greetaft": case "greeteve": case "greetnightfall": case "greetbloodmoon":
                    category=Met.Contains(trader)?"return":"greeting";break;
                case "quest_offer":category="job_offer";break;
                case "quest_declined":category="job_decline";break;
                case "sale_accepted":case "sale_declined":category="farewell";break;
                case "announce_closing":category="closing_warning";break;
                case "announce_closed":category="closing_final";break;
            }
            if (category == null) Trace(trader+" fallback=unmapped-event event="+evt);
            if(category!=null && Prepare(trader,category,sound,out var replacement))
            { sound=replacement; if(category=="greeting"||category=="return")Met.Add(trader); }
            return;
        }
    }
}

[Preserve]
public sealed class RebirthTraderVoicesModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        RebirthTraderVoices.Root=modInstance.Path;
        var harmony=new Harmony("rebirth.trader.voices");
        foreach(var type in new[]{typeof(RebirthTraderVoiceHeadPatch),typeof(RebirthTraderVoicePositionPatch),typeof(RebirthTraderVoiceEntityPatch),
            typeof(RebirthTraderVoiceBuyPatch),typeof(RebirthTraderVoiceSellPatch),typeof(RebirthTraderVoiceBrowsePatch),
            typeof(RebirthTraderVoiceQuestCompletePatch),typeof(RebirthTraderVoiceQuestAcceptPatch),typeof(RebirthTraderVoicePrematurePatch),
            typeof(RebirthTraderVoiceDialogPatch)}) RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
    }
}
