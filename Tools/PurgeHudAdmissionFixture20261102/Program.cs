using System;
class RebirthJournalEntry {}
class GUIWindow {}
class Manager {public bool Hidden;public int Attempts,Callbacks;public bool IsFullHUDDisabled()=>Hidden;public void Open(GUIWindow w,bool modal,bool noEscape){Attempts++;if(Hidden)return;Callbacks++;}}
class PlayerUi {public Manager windowManager=new();}
class XUi {public PlayerUi playerUI=new();public XUiC_RebirthPurgeInformation Controller=new();public T GetChildByType<T>() where T:class=>Controller as T;}
partial class XUiC_RebirthPurgeInformation {public GUIWindow windowGroup=new();RebirthJournalEntry entry;string sprite;bool manualReading;}
class Program {
 static int checks;static void Check(bool v,string label){checks++;if(!v)throw new Exception(label);}
 static bool OldOpen(XUi ui){ui.playerUI.windowManager.Open(ui.Controller.windowGroup,true,true);return true;}
 static void Main(){var u=new XUi();u.playerUI.windowManager.Hidden=true;
 Check(OldOpen(u)&&u.playerUI.windowManager.Callbacks==0,"old wrapper falsely admits native hidden HUD refusal");
 int attempts=u.playerUI.windowManager.Attempts;
 Check(!XUiC_RebirthPurgeInformation.Open(u,new(),"art"),"hidden HUD refusal propagated");
 Check(u.playerUI.windowManager.Attempts==attempts,"refused request never submitted to native manager");
 u.playerUI.windowManager.Hidden=false;
 Check(XUiC_RebirthPurgeInformation.Open(u,new(),"art"),"next-frame HUD admission succeeds without waiting two seconds");
 Check(u.playerUI.windowManager.Callbacks==1,"one admitted native callback");
 u.Controller.windowGroup=null;Check(!XUiC_RebirthPurgeInformation.Open(u,new(),"art"),"missing group refused");
 u.Controller=null;Check(!XUiC_RebirthPurgeInformation.Open(u,new(),"art"),"missing controller refused");
 Check(!XUiC_RebirthPurgeInformation.Open(null,new(),"art"),"missing UI refused");
 Console.WriteLine("PASS "+checks+" extracted production HUD admission checks; native window manager doubled. First-entry HUD restoration needs native fresh-spawn retest.");}}
