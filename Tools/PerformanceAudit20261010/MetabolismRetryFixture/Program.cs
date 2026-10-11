using System; using System.Collections.Generic; using System.IO; using System.Xml.Linq;
class RebirthMetabolismState { public string Id; }
static class Clock {public static long Now; public static long Frequency=1000; public static long GetTimestamp()=>Now;}
static class Log {public static int Errors; public static void Error(string s){Errors++;} public static void Out(string s){} }
static class RebirthLogSettings {public static bool AutomaticLoggingDefault=false;}
class Program {
    private const int PersistenceIngestionSafetyCeiling = 1024;
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, RebirthMetabolismState> States =
        new Dictionary<string, RebirthMetabolismState>(StringComparer.Ordinal);
    private static bool loaded;
    private static bool serverAuthority;
    private const int LoadRetrySeconds = 5;
    private static long nextLoadAttempt;
    private static string failedLoadPath;
    private static int loadGeneration;

    public static void Reset(bool asServer)
    {
        lock (Sync)
        {
            States.Clear();
            loaded = false;
            nextLoadAttempt = 0;
            failedLoadPath = null;
            unchecked { loadGeneration++; }
            serverAuthority = asServer;
        }
    }

    private static bool EnsureLoaded()
    {
        if (loaded)
            return true;

        string path;
        bool authority;
        int generation;
        lock (Sync)
        {
            if (loaded) return true;
            path = PathName;
            authority = serverAuthority;
            generation = loadGeneration;
            // A damaged file must remain fail-closed, without reparsing and logging
            // on every player/update request. A new path or Reset retries immediately.
            if (string.Equals(failedLoadPath, path, StringComparison.Ordinal) &&
                Clock.GetTimestamp() < nextLoadAttempt)
                return false;
            if (!authority || string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                States.Clear();
                loaded = true;
                return true;
            }
        }

        try
        {
            XDocument doc = XDocument.Load(path);
            XElement root = doc.Root;
            if (root == null)
                throw new InvalidDataException("metabolism persistence root is missing");

            Dictionary<string, RebirthMetabolismState> candidate =
                new Dictionary<string, RebirthMetabolismState>(StringComparer.Ordinal);
            foreach (XElement node in root.Elements("player"))
            {
                string id = A(node, "id", string.Empty);
                if (string.IsNullOrEmpty(id))
                    throw new InvalidDataException("metabolism persistence player id is missing");
                RebirthMetabolismState state = DeserializePlayer(node);
                if (state == null)
                    throw new InvalidDataException("metabolism persistence player could not be decoded: " + id);
                candidate[id] = state;
            }

            lock (Sync)
            {
                if (generation != loadGeneration) return false;
                if (loaded) return true;
                nextLoadAttempt = 0;
                failedLoadPath = null;
                States.Clear();
                foreach (KeyValuePair<string, RebirthMetabolismState> pair in candidate)
                    States[pair.Key] = pair.Value;
                loaded = true;
            }
            { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Metabolism] persistence loaded players=" + candidate.Count); }
            return true;
        }
        catch (Exception ex)
        {
            // Never publish a partial candidate or overwrite the unreadable file.
            // Ignore an old world's failure after Reset, including its retry deadline.
            lock (Sync)
            {
                if (generation != loadGeneration || loaded) return false;
                failedLoadPath = path;
                nextLoadAttempt = Clock.GetTimestamp() +
                    LoadRetrySeconds * Clock.Frequency;
            }
            Log.Error("[REBIRTH Metabolism] persistence load failed: " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }
static string PathName; static Action OnDecode;
static string A(XElement e,string n,string f)=>(string)e.Attribute(n)??f;
static RebirthMetabolismState DeserializePlayer(XElement e){var action=OnDecode;OnDecode=null;action?.Invoke(); if((string)e.Attribute("bad")=="1")throw new InvalidDataException("bad record"); return new(){Id=(string)e.Attribute("id")};}
static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
static void Fresh(string path){Reset(true);PathName=path;Clock.Now=0;Log.Errors=0;OnDecode=null;}
static void Main(){string dir=Path.Combine(Path.GetTempPath(),"rebirth-metabolism-retry-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir); string path=Path.Combine(dir,"players.xml");
try {
Fresh(path);File.WriteAllText(path,"<broken"); Check(!EnsureLoaded()&&Log.Errors==1&&!loaded,"invalid XML fails closed");
for(int i=0;i<10000;i++)CheckQuiet(!EnsureLoaded());Check(Log.Errors==1&&States.Count==0,"10000 immediate requests do not repeat errors or publish state");
File.WriteAllText(path,"<players><player id='restored'/></players>");Clock.Now=4999;Check(!EnsureLoaded(),"repair waits until retry boundary");Clock.Now=5000;Check(EnsureLoaded()&&States.ContainsKey("restored"),"repair loads at exact deadline");
File.WriteAllText(path,"bad");Check(EnsureLoaded()&&Log.Errors==1,"success remains cached");
Fresh(path);Check(!EnsureLoaded(),"failure before reset");File.WriteAllText(path,"<players><player id='reset'/></players>");Reset(true);Check(EnsureLoaded()&&States.ContainsKey("reset"),"reset clears retry delay immediately");
Fresh(path);File.WriteAllText(path,"bad");Check(!EnsureLoaded(),"failure before path change");PathName=Path.Combine(dir,"other.xml");File.WriteAllText(PathName,"<players><player id='other'/></players>");Check(EnsureLoaded()&&States.ContainsKey("other"),"new path bypasses old delay");
Fresh(path);File.WriteAllText(path,"<players><player id='first'/><player id='bad' bad='1'/></players>");Check(!EnsureLoaded()&&States.Count==0,"decode failure never publishes partial records");
Fresh(path);File.WriteAllText(path,"<players><player id='old'/></players>");OnDecode=()=>Reset(true);Check(!EnsureLoaded()&&!loaded&&States.Count==0,"old successful load cannot publish after reset");Check(EnsureLoaded(),"new generation can load immediately");
Fresh(path);File.WriteAllText(path,"<players><player id='old' bad='1'/></players>");OnDecode=()=>Reset(true);Check(!EnsureLoaded()&&Log.Errors==0&&nextLoadAttempt==0,"old failed load cannot throttle or log new generation");
Fresh(Path.Combine(dir,"missing.xml"));Check(EnsureLoaded()&&States.Count==0,"missing initial file retains default behavior");
Console.WriteLine("Scope: extracted production load/reset code; actual XML/files, fake monotonic clock and player decoder. No real saves or Unity runtime used.");
} finally {Directory.Delete(dir,true);} }
static void CheckQuiet(bool ok){if(!ok)throw new Exception("unexpected load");}
}
