using System;
// Only the native-independent display DTO is doubled; actual cache/scope are linked.
public sealed class RebirthBackpackLibraryView { public string CreationId; public long GearRevision; }
static class Program {
 static int passed;
 static void Check(bool value,string label){if(!value)throw new Exception(label);passed++;}
 static void Main(){
  var session=new object();var creation=Guid.NewGuid().ToString("N");
  var cache=new RebirthBackpackLibraryViewCache(session,creation);
  Check(!cache.TryGetRevision(session,creation,out var revision)&&revision==-1,"fresh cache cannot invent revision zero");
  var request=Guid.NewGuid();Check(cache.BeginRequest(session,creation,request),"request admitted");
  Check(!cache.ReceiveNoBackpack(session,creation,Guid.NewGuid(),2),"uncorrelated no backpack reply refused");
  Check(!cache.TryGetRevision(session,creation,out revision)&&revision==-1,"refused reply grants no hint");
  Check(cache.ReceiveNoBackpack(session,creation,request,2),"correlated no backpack reply");
  Check(cache.TryGetRevision(session,creation,out revision)&&revision==2,"first equipment revision available without storage contents");
  Check(!cache.TryGetRevision(new object(),creation,out revision)&&revision==-1,"old native session refuses hint");
  Check(!cache.TryGetRevision(session,Guid.NewGuid().ToString("N"),out revision)&&revision==-1,"other character refuses hint");
  request=Guid.NewGuid();Check(cache.BeginRequest(session,creation,request),"refresh admitted");
  Check(!cache.ReceiveNoBackpack(session,creation,request,1),"revision rollback refused");
  Check(cache.TryGetRevision(session,creation,out revision)&&revision==2,"failed rollback preserves accepted hint");
  Check(cache.Receive(session,creation,request,new RebirthBackpackLibraryView{CreationId=creation,GearRevision=3}),"ready view replaces no backpack");
  Check(cache.TryGetRevision(session,creation,out revision)&&revision==3,"equipped ready revision available");
  cache.Reset();Check(!cache.TryGetRevision(session,creation,out revision)&&revision==-1,"reset revokes advisory hint");
  var legacy="legacy-"+new string('a',64);cache=new RebirthBackpackLibraryViewCache(session,legacy);request=Guid.NewGuid();
  Check(cache.BeginRequest(session,legacy,request)&&cache.ReceiveNoBackpack(session,legacy,request,0)&&cache.TryGetRevision(session,legacy,out revision)&&revision==0,"full legacy migrated identity supported");
  Console.WriteLine($"PASS {passed} actual revision cache/scope checks; display DTO doubled; no native network or inventory validation");
 }
}