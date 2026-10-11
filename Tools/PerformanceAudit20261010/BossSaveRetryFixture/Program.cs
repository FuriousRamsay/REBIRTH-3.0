using System;
using System.IO;
class World { public bool Remote; public bool IsRemote()=>Remote; }
static class Time { public static float realtimeSinceStartup; }
class Program {
 const float SaveIntervalSeconds=60f;
 static bool dirty; static float nextSave; static int attempts; static bool fail; static float duration;
 static void Save(){ attempts++; Time.realtimeSinceStartup+=duration; if(fail)throw new IOException("fixture write failure"); dirty=false; }
 public static void SaveIfDue(World world)
    {
        if (!dirty || world == null || world.IsRemote() || Time.realtimeSinceStartup < nextSave) return;
        // Failed writes must respect the same cadence; Save leaves dirty set on failure.
        // Explicit shutdown saves still call Save directly and bypass this delay.
        try { Save(); }
        finally { nextSave = Time.realtimeSinceStartup + SaveIntervalSeconds; }
    }

 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name); checks++; Console.WriteLine("PASS "+name);}
 static void Main(){
 var server=new World(); var remote=new World{Remote=true};
 dirty=true;fail=true;Time.realtimeSinceStartup=10;
 bool threw=false;try{SaveIfDue(server);}catch(IOException){threw=true;}
 Check(threw&&attempts==1&&dirty,"failure propagates and preserves dirty data");
 Check(nextSave==70,"failed attempt advances existing interval");
 for(int i=0;i<10000;i++)SaveIfDue(server);
 Check(attempts==1,"10000 requests do not retry inside interval");
 Time.realtimeSinceStartup=69.999f;SaveIfDue(server);Check(attempts==1,"deadline exclusive before boundary");
 Time.realtimeSinceStartup=70;fail=false;SaveIfDue(server);Check(attempts==2&&!dirty&&nextSave==130,"repair saves at exact deadline");
 Time.realtimeSinceStartup=200;SaveIfDue(server);Check(attempts==2,"clean state does not write");
 dirty=true;SaveIfDue(null);SaveIfDue(remote);Check(attempts==2&&dirty&&nextSave==130,"null and remote worlds do not write or postpone");
 duration=5;SaveIfDue(server);Check(attempts==3&&nextSave==265,"successful delay remains measured from completion");
 dirty=true;fail=true;Time.realtimeSinceStartup=265;try{SaveIfDue(server);}catch(IOException){}
 Check(attempts==4&&dirty&&nextSave==330,"failed slow write delays from completion");
 fail=false;Save();Check(attempts==5&&!dirty&&Time.realtimeSinceStartup<nextSave,"explicit save bypasses pending retry delay");
 Console.WriteLine(checks+" checks passed; isolated scheduling fixture, no Unity or disk failure injection.");
 }
}
