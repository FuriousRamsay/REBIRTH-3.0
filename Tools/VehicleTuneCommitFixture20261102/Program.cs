using System;
class Control {public float Value;public bool Proposal; public void Apply(float n){Value=n;}public void ApplyText(string s){Value=float.Parse(s,System.Globalization.CultureInfo.InvariantCulture);}}
class Session {public bool Valid=true;}
class Input {public string Text;}
static class RebirthVehicleTuningUi {public static Session Session;}
static class Localization {public static string Get(string s)=>s;}
partial class Tuner {
 bool refreshing;int epoch=2;int[] editEpoch={2,2,2,2};string[] editText={"5","5","5","5"};
 Control[] editing=new Control[4],displayed=new Control[4];Input[] inputs={new(),new(),new(),new()};
 Session session;int heldSlider=-1;Control heldControl;float heldNumber;string status;
 void NeedSession(){if(!session.Valid)throw new Exception();}void ReleaseSlider(){heldSlider=-1;heldControl=null;}
 static int checks;static void Check(bool yes,string why){checks++;if(!yes)throw new Exception(why);}
 static Tuner New(){var t=new Tuner();t.session=new();RebirthVehicleTuningUi.Session=t.session;for(int i=0;i<4;i++){t.editing[i]=t.displayed[i]=new Control{Value=5};t.inputs[i].Text="5";}return t;}
 static void Main(){
  foreach(var exit in new[]{"blur","Drive","Close","Escape","F8","category","scroll","export"}){
   var t=New();t.inputs[0].Text="16.78453";t.CommitEdits();Check(t.displayed[0].Value==16.78453f,exit+" typed commit");
   t.inputs[0].Text="23.498342";t.CommitEdits();Check(t.displayed[0].Value==23.498342f,exit+" repeated edit");
  }
  {var t=New();t.editEpoch[0]=1;t.inputs[0].Text="80";t.CommitEdits();Check(t.displayed[0].Value==5,"stale epoch rejected");}
  {var t=New();t.editing[0]=new();t.inputs[0].Text="80";t.CommitEdits();Check(t.displayed[0].Value==5,"recycled owner rejected");}
  {var t=New();t.session.Valid=false;t.inputs[0].Text="80";t.CommitEdits();Check(t.displayed[0].Value==5,"invalid session rejected");}
  {var t=New();t.heldSlider=0;t.heldControl=t.displayed[0];t.heldNumber=44;t.CommitEdits();Check(t.displayed[0].Value==44&&t.heldSlider==-1,"last slider sample committed");}
  {var t=New();t.displayed[0].Value=5.1234567f;t.CommitEdits();Check(t.displayed[0].Value==5.1234567f,"untouched rounded display never committed");}
  {var t=New();t.inputs[0].Text="bad";try{t.CommitEdits();throw new Exception("accepted invalid");}catch(FormatException){}Check(t.displayed[0].Value==5,"invalid value rejected");}
  Console.WriteLine("PASS "+checks+" extracted production commit checks. UI event delivery/native drive not tested.");
 }
}
