using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

#nullable disable

public static class RebirthNpcProgressionPersistenceStore
{
    private const int SchemaVersion=2; private static readonly object Sync=new object(); private static bool loaded,dirty; private static long saves,loads,repairs; private static string lastError=string.Empty;
    public static void EnsureLoaded()
    {
        if (!IsServer() || GameManager.Instance?.World == null) return;
        lock (Sync)
        {
            if (loaded) return;
            string p = GetPath();
            if (string.IsNullOrEmpty(p)) return;
            if (!File.Exists(p) && !File.Exists(p + ".bak"))
            { RebirthNpcPersistenceFile.AssertWritable(p); loaded = true; return; }
            try
            {
                string source = File.Exists(p) ? p : p + ".bak";
                try { ProgressionFileData data = Read(source); RebirthNpcProgressionService.ImportRecords(data.Records, data.ReplayIds, true); }
                catch
                {
                    if (source.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) || !File.Exists(p + ".bak")) throw;
                    source = p + ".bak";
                    ProgressionFileData data = Read(source); RebirthNpcProgressionService.ImportRecords(data.Records, data.ReplayIds, true);
                }
                if (source == p) loads++; else repairs++;
                RebirthNpcPersistenceFile.VerifiedRead(p);
                loaded = true;
            }
            catch (Exception ex)
            {
                RebirthNpcPersistenceFile.BlockWrite(p, ex.GetType().Name + ": " + ex.Message);
                throw;
            }
        }
    }
    public static void MarkDirty(){if(IsServer())dirty=true;}
    public static void Save(){if(!IsServer())return;EnsureLoaded();lock(Sync){if(!dirty && !RebirthNpcPersistenceCoordinator.IsCheckpointWrite)return;string p=GetPath();RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded,p);RebirthNpcPersistenceFile.AssertWritable(p);if(string.IsNullOrEmpty(p))return;Directory.CreateDirectory(Path.GetDirectoryName(p));string tmp=p+".tmp",bak=p+".bak";Write(tmp,RebirthNpcProgressionService.ExportRecords(),RebirthNpcProgressionService.ExportReplayIds());if(File.Exists(p))File.Copy(p,bak,true);if(File.Exists(p))File.Delete(p);File.Move(tmp,p);dirty=false;saves++;lastError=string.Empty;}}
    public static void Reset(bool save){lock(Sync){if(save)Save();loaded=false;dirty=false;lastError=string.Empty;}}
    public static string ExportTo(string path){if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("Export path is required.");Write(path,RebirthNpcProgressionService.ExportRecords(),RebirthNpcProgressionService.ExportReplayIds());return path;}
    public static int ImportFrom(string path,bool replace){if(!IsServer())throw new InvalidOperationException("Import requires server authority.");ProgressionFileData data=Read(path);RebirthNpcProgressionService.ImportRecords(data.Records,data.ReplayIds,replace);dirty=true;return data.Records.Count;}
    public static string GetReport(){return "[REBIRTH NPC Progression Persistence] loaded="+loaded+" dirty="+dirty+" loads="+loads+" saves="+saves+" repairs="+repairs+" error="+(string.IsNullOrEmpty(lastError)?"none":lastError);}
    private static string GetPath(){string d=GameIO.GetSaveGameDir();return string.IsNullOrEmpty(d)?string.Empty:Path.Combine(d,"RebirthNpcProgression.xml");}
    private sealed class ProgressionFileData{public readonly List<RebirthNpcProgressionRecord> Records=new List<RebirthNpcProgressionRecord>();public readonly List<Guid> ReplayIds=new List<Guid>();}
    private static void Write(string path,RebirthNpcProgressionRecord[] records,Guid[] replayIds){using(var w=XmlWriter.Create(path,new XmlWriterSettings{Indent=true,Encoding=new System.Text.UTF8Encoding(false)})){w.WriteStartDocument();w.WriteStartElement("rebirthNpcProgression");w.WriteAttributeString("schema",SchemaVersion.ToString(CultureInfo.InvariantCulture));foreach(var r in records){w.WriteStartElement("npc");w.WriteAttributeString("id",r.NpcId.ToString());w.WriteAttributeString("revision",r.ComponentRevision.ToString(CultureInfo.InvariantCulture));foreach(var e in r.Entries.Values){w.WriteStartElement("entry");w.WriteAttributeString("id",e.ProgressionId);w.WriteAttributeString("xp",e.CumulativeXp.ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("level",e.CachedLevel.ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("curve",e.AppliedCurveVersion.ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("revision",e.EntryRevision.ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("lastTicks",e.LastAwardUtcTicks.ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("lastAward",e.LastAwardId.ToString("N"));w.WriteEndElement();}w.WriteEndElement();}foreach(Guid id in replayIds??new Guid[0]){if(id==Guid.Empty)continue;w.WriteStartElement("replay");w.WriteAttributeString("id",id.ToString("N"));w.WriteEndElement();}w.WriteEndElement();w.WriteEndDocument();}}
    private static ProgressionFileData Read(string path){var result=new ProgressionFileData();var doc=new XmlDocument();doc.Load(path);XmlElement root=doc.DocumentElement;if(root==null||root.Name!="rebirthNpcProgression")throw new InvalidDataException("Invalid progression root.");int schema=Int(Attr(root,"schema"));if(schema<1||schema>SchemaVersion)throw new InvalidDataException("Unsupported progression schema "+schema+".");foreach(XmlNode n in root.SelectNodes("npc")){RebirthNpcStableId id;if(!RebirthNpcStableId.TryParse(Attr(n,"id"),out id))continue;var r=new RebirthNpcProgressionRecord{NpcId=id,ComponentRevision=Long(Attr(n,"revision"))};foreach(XmlNode x in n.SelectNodes("entry")){string key=Attr(x,"id");if(string.IsNullOrWhiteSpace(key))continue;Guid award;Guid.TryParseExact(Attr(x,"lastAward"),"N",out award);r.Entries[key]=new RebirthNpcProgressionEntryRecord{ProgressionId=key,CumulativeXp=Long(Attr(x,"xp")),CachedLevel=Int(Attr(x,"level")),AppliedCurveVersion=Int(Attr(x,"curve")),EntryRevision=Long(Attr(x,"revision")),LastAwardUtcTicks=Long(Attr(x,"lastTicks")),LastAwardId=award};}result.Records.Add(r);}if(schema>=2)foreach(XmlNode n in root.SelectNodes("replay")){Guid id;if(Guid.TryParseExact(Attr(n,"id"),"N",out id)&&id!=Guid.Empty)result.ReplayIds.Add(id);}return result;}
    private static string Attr(XmlNode n,string k){XmlAttribute a=n.Attributes[k];return a==null?string.Empty:a.Value;} private static long Long(string s){long v;return long.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:0;} private static int Int(string s){int v;return int.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:0;}
    private static bool IsServer(){ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;return c==null||c.IsServer;}
}
