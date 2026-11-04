using Audio;
int checks=0, failures=0;
void Check(bool condition,string reason){checks++;if(!condition){failures++;System.Console.WriteLine("FAIL "+reason);}}
RebirthTraderVoices.Root=System.IO.Path.GetFullPath(args[0]);
foreach(string trader in RebirthTraderVoicePreferences.Traders)
{
 var npc=new EntityTrader{NPCInfo=new NPCInfo{Id=trader=="Joel"?"traitorjoel":"trader"+trader.ToLowerInvariant()}};
 string original="trader_"+trader.ToLowerInvariant()+"_greeting";
 foreach(string evt in new[]{"greeting","quest_accepted","quest_complete"})
  Manager.audioData["trader_"+trader.ToLowerInvariant()+"_"+evt]=new XmlData{audioClipMap={new ClipSourceMap{audioSourceName="native-prefab"}}};
 for(int preset=0;preset<7;preset++)
 {
  RebirthTraderVoicePreferences.Selection=preset; TraderInfo.TraderDialog=false;
  DataLoader.Loads=0;Manager.Plays=0;
  bool prepared=RebirthTraderVoices.Prepare(trader,"greeting",original,out var replacement);
  Check(!prepared&&replacement==original&&DataLoader.Loads==0,"disabled Prepare "+trader+"/"+preset);
  RebirthTraderVoices.Play(npc,"purchase"); Check(Manager.Plays==0,"disabled event "+trader+"/"+preset);
  RebirthTraderVoices.PlayConfirmed(npc,"job_accept","quest_accepted");
  RebirthTraderVoices.PlayConfirmed(npc,"job_complete","quest_complete");
  Check(Manager.Plays==0,"disabled confirmed "+trader+"/"+preset);
  string routed=original;RebirthTraderVoices.Route(ref routed);Check(routed==original,"disabled route "+trader+"/"+preset);
  TraderInfo.TraderDialog=true;Manager.Plays=0;
  prepared=RebirthTraderVoices.Prepare(trader,"greeting",original,out replacement);
  Check(preset==0?!prepared&&replacement==original:prepared&&replacement!=original,"enabled selection "+trader+"/"+preset);
  RebirthTraderVoices.Play(npc,"purchase");Check(Manager.Plays==(preset==0?0:1),"enabled event "+trader+"/"+preset);
  RebirthTraderVoices.PlayConfirmed(npc,"missing-category","quest_accepted");
  Check(Manager.Plays==(preset==0?0:2),"enabled native fallback "+trader+"/"+preset);
 }
}
TraderInfo.TraderDialog=true;RebirthTraderVoicePreferences.Selection=1;
GameManager.IsDedicatedServer=true;
Check(!RebirthTraderVoices.Prepare("Hugh","greeting","trader_hugh_greeting",out _),"dedicated guard retained");
GameManager.IsDedicatedServer=false;LocalPlayerUI.Ready=false;
Check(!RebirthTraderVoices.Prepare("Hugh","greeting","trader_hugh_greeting",out _),"local ready guard retained");
System.Console.WriteLine($"Checks={checks}; failures={failures}");return failures==0?0:1;
