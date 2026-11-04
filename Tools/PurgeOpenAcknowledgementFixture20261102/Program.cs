using System;
class Pending {public float RequestedAt;public int Attempts;public bool Acknowledged;public object Entry,Sprite;public bool Manual;}
class Ui {public object xui;}
static class XUiC_RebirthPurgeInformation {public static Func<bool> OnOpen;public static bool Open(object ui,object entry,object sprite,bool manual)=>OnOpen();}
partial class Harness {
 public Pending Head=new();public int Blocked;string lastBlockedReason;float nextBlockedTrace;void TraceBlocked(Pending r,string reason,float now){Blocked++;}
 void Acknowledge(){if(Head.RequestedAt>0)Head.Acknowledged=true;}
 static int checks;static void Check(bool v,string text){checks++;if(!v)throw new Exception(text);}
 static void Main(){
  var old=new Harness();XUiC_RebirthPurgeInformation.OnOpen=()=>{old.Acknowledge();return true;};XUiC_RebirthPurgeInformation.Open(null,null,null,false);old.Head.RequestedAt=42;Check(!old.Head.Acknowledged,"baseline reproduces lost synchronous acknowledgement");
  var h=new Harness();XUiC_RebirthPurgeInformation.OnOpen=()=>{h.Acknowledge();return true;};h.Submit();Check(h.Head.Acknowledged,"synchronous native OnOpen acknowledged");Check(h.Head.Attempts==1&&h.Head.RequestedAt==42,"one attempt armed");
  var deferred=new Harness();XUiC_RebirthPurgeInformation.OnOpen=()=>true;deferred.Submit();Check(!deferred.Head.Acknowledged,"deferred not fabricated");deferred.Acknowledge();Check(deferred.Head.Acknowledged,"deferred native OnOpen still accepted");
  var refused=new Harness();refused.Head.RequestedAt=12;refused.Head.Attempts=2;XUiC_RebirthPurgeInformation.OnOpen=()=>false;refused.Submit();Check(refused.Head.RequestedAt==12&&refused.Head.Attempts==2,"refusal restores prior attempt state");Check(refused.Blocked==1,"refusal diagnostic retained");
  Console.WriteLine("PASS "+checks+" extracted production opening-block checks, including baseline failure and both native callback orders.");
 }
}
