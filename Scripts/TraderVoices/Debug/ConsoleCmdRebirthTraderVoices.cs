using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.Scripting;

[Preserve]
public sealed class ConsoleCmdRebirthTraderVoices : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    public override string[] getCommands() => new[]{"rbvoices"};
    public override string getDescription() => "Trader voice diagnostics: status | log on/off | set Trader 0..6 | test Trader category";
    public override void Execute(List<string> args,CommandSenderInfo senderInfo)
    {
        if(args.Count>=2 && args[0]=="log")RebirthTraderVoices.Diagnostics=args[1]=="on";
        if(args.Count>=3 && args[0]=="set")
        {
            int value;
            if(!RebirthTraderVoicePreferences.ValidTrader(args[1]) || !int.TryParse(args[2],out value))throw new ArgumentException("Expected vanilla trader and index");
            RebirthTraderVoicePreferences.Set(args[1],value);
        }
        if(args.Count>=3 && args[0]=="test")
        {
            string original="trader_"+args[1].ToLowerInvariant()+"_greeting";
            bool replaced=RebirthTraderVoices.Prepare(args[1],args[2],original,out var sound);
            Audio.Manager.PlayInsidePlayerHead(sound,-1,0,false,true);
            SdtdConsole.Instance.Output("replacement="+replaced+" sound="+sound);
        }
        var headMethod = AccessTools.Method(typeof(Audio.Manager), nameof(Audio.Manager.PlayInsidePlayerHead),
            new[] { typeof(string), typeof(int), typeof(float), typeof(bool), typeof(bool) });
        var headPatches = headMethod != null ? Harmony.GetPatchInfo(headMethod) : null;
        bool headHook = false;
        if (headPatches != null)
            foreach (var prefix in headPatches.Prefixes)
                if (prefix.PatchMethod.DeclaringType == typeof(RebirthTraderVoiceHeadPatch)) headHook = true;
        SdtdConsole.Instance.Output("voice-head-hook=" + headHook);
        var world=GameManager.Instance?.World;
        SdtdConsole.Instance.Output("voice-context dedicated="+GameManager.IsDedicatedServer+
            " remote="+(world!=null && world.IsRemote())+
            " localPlayerReady="+(LocalPlayerUI.GetUIForPrimaryPlayer()?.entityPlayer!=null)+
            " diagnostics="+RebirthTraderVoices.Diagnostics+
            " cataloguePresent="+System.IO.File.Exists(System.IO.Path.Combine(RebirthTraderVoices.Root ?? string.Empty,"Config","_TraderVoices","catalogue.xml"))+
            " bundlePresent="+System.IO.File.Exists(System.IO.Path.Combine(RebirthTraderVoices.Root ?? string.Empty,"Resources","rebirth_trader_voices.unity3d")));
        foreach(string trader in RebirthTraderVoicePreferences.Traders)
            SdtdConsole.Instance.Output(trader+"="+RebirthTraderVoicePreferences.Presets[RebirthTraderVoicePreferences.Get(trader)]);
        SdtdConsole.Instance.Output("last="+RebirthTraderVoices.LastLineId+" text="+RebirthTraderVoices.LastText);
        foreach(string line in RebirthTraderVoices.RecentEvents)SdtdConsole.Instance.Output(line);
    }
}
