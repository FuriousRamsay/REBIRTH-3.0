using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

#nullable disable

public static partial class RebirthNpcProgressionPersistenceStore
{
    private static RebirthNpcOriginalSaveBinding originalBinding;
    private static void CaptureOriginalBinding()=>originalBinding=RebirthNpcOriginalSaveBinding.Capture(boundWorld as World,boundPath,RebirthNpcProgressionService.RecoveryEpoch);
    private const int SchemaVersion=2; private static readonly object Sync=new object(); private static bool loaded,dirty,flushing,loadFailed;private static long changeVersion; private static string boundPath; private static WorldBase boundWorld; private static long saves,loads,repairs; private static string lastError=string.Empty;
    public static void EnsureLoaded()
    {
        if (!IsServer() || GameManager.Instance?.World == null || GameManager.Instance.World.IsRemote()) return;
        lock (Sync)
        {
            if(boundPath!=null&&!flushing&&(!ReferenceEquals(boundWorld,GameManager.Instance.World)||!string.Equals(boundPath,CurrentPath(),StringComparison.Ordinal))) throw new InvalidOperationException("Unresolved progression scope requires retirement before world change.");
            if (loaded)
            {
                if(!flushing&&(!ReferenceEquals(boundWorld,GameManager.Instance.World)||!string.Equals(boundPath,CurrentPath(),StringComparison.Ordinal)))
                    throw new InvalidOperationException("Progression store belongs to a previous world; flush/reset required.");
                return;
            }
            string p = CurrentPath();
            if (string.IsNullOrEmpty(p)) return;
            boundPath=p;boundWorld=GameManager.Instance.World;loadFailed=false;
            if (!File.Exists(p) && !File.Exists(p + ".bak"))
            { if(!RebirthNpcPersistenceFile.CanInitializeEmpty(p)){loadFailed=true;throw new InvalidDataException("Progression recovery unresolved.");} RebirthNpcPersistenceFile.AssertWritable(p); RebirthNpcProgressionService.ImportRecords(new RebirthNpcProgressionRecord[0],new Guid[0],true); loaded = true; CaptureOriginalBinding();return; }
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
                loaded = true;CaptureOriginalBinding();
            }
            catch (Exception ex)
            {
                loadFailed=true; RebirthNpcPersistenceFile.BlockWrite(p, ex.GetType().Name + ": " + ex.Message);
                throw;
            }
        }
    }
    public static void MarkDirty(){if(IsServer()){if(!ThreadManager.IsMainThread()){System.Threading.Interlocked.Increment(ref changeVersion);dirty=true;originalBinding?.InvalidateMutation();return;}lock(Sync){System.Threading.Interlocked.Increment(ref changeVersion);dirty=true;originalBinding?.ObserveMutation(RebirthNpcProgressionService.RecoveryEpoch);}}}
    public static void Save()
    {
        if(!IsServer())return;EnsureLoaded();
        lock(Sync)
        {
            if(!dirty&&!RebirthNpcPersistenceCoordinator.IsCheckpointWrite)return;
            string p=GetPath();RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded,p);RebirthNpcPersistenceFile.AssertWritable(p);
            if(!loaded||loadFailed)throw new InvalidOperationException("Progression load is unresolved.");
            using var originalWriter=RebirthNpcPersistenceFile.BeginWrite(p);
            long snapshotVersion=System.Threading.Interlocked.Read(ref changeVersion);
            if(RebirthNpcRecoveryPublications.IsInstalled(p))
            {
                RebirthNpcProgressionService.CaptureRetirement(out var rows,out var replay,out var epoch);
                var document=SnapshotDocument(rows,replay);ReadDocument(document);
                if(!ReferenceEquals(epoch,RebirthNpcProgressionService.RecoveryEpoch))throw new InvalidOperationException("Progression snapshot changed before publication.");
                RebirthNpcPersistenceFile.RequireCurrentWriter(p);
                if(!RebirthNpcRecoveryPublications.TryPublishInstalled(p,document,out bool handled,out var error)||!handled)
                    throw new IOException("Progression installed recovery publication retained: "+error);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(p));string tmp=p+".tmp";
                Write(tmp,RebirthNpcProgressionService.ExportRecords(),RebirthNpcProgressionService.ExportReplayIds());Read(tmp);
                RebirthNpcPersistenceFile.RequireCurrentWriter(p);
                string error;if(!RebirthDurableFileCommit.TryPublish(tmp,p,out error))throw new IOException(error);
            }
            dirty=System.Threading.Interlocked.Read(ref changeVersion)!=snapshotVersion;saves++;CaptureOriginalBinding();lastError=string.Empty;
        }
    }
    public static bool CanReset
    {
        get{lock(Sync)try
        {
            if(dirty||loadFailed||!RebirthNpcProgressionProjectionRevisionService.CanRetireEmptyRevisions)return false;
            RebirthNpcProgressionService.CaptureRetirement(out var rows,out var replay,out var epoch);
            if(boundPath==null)return !loaded&&rows.Length==0&&replay.Length==0;
            if(!loaded||RebirthNpcPersistenceFile.IsRecoveryFenced(boundPath))return false;
            if(!File.Exists(boundPath))return rows.Length==0&&replay.Length==0&&RebirthNpcPersistenceFile.CanInitializeEmpty(boundPath);
            var primary=LoadPrimary(boundPath);var saved=ReadDocument(primary);
            return SnapshotDocument(saved.Records.ToArray(),saved.ReplayIds.ToArray()).DocumentElement.OuterXml==SnapshotDocument(rows,replay).DocumentElement.OuterXml&&ReferenceEquals(epoch,RebirthNpcProgressionService.RecoveryEpoch);
        }catch{return false;}}
    }
    public static bool TryFlush()
    {
        lock(Sync)try
        {
            if(boundPath==null)return CanReset;
            if(ReferenceEquals(boundWorld,GameManager.Instance?.World)&&boundPath==CurrentPath()){Save();return CanReset;}
            if(!dirty)return CanReset;
            if(!loaded||loadFailed||originalBinding==null)return false;
            var epoch=RebirthNpcProgressionService.RecoveryEpoch;
            
            if(!ReferenceEquals(epoch,RebirthNpcProgressionService.RecoveryEpoch)||!originalBinding.TryPublish(()=>{RebirthNpcProgressionService.CaptureRetirement(out var capturedRows,out var capturedReplay,out _);return SnapshotDocument(capturedRows,capturedReplay);},doc=>{ReadDocument(doc);return true;},()=>RebirthNpcProgressionService.RecoveryEpoch,out var error))return false;
            dirty=false;saves++;return CanReset;
        }catch(Exception ex){lastError=ex.Message;return false;}
    }
    public static bool TryReset(bool save)
    {
        lock(Sync)
        {
            if(save&&!TryFlush())return false;if(!CanReset)return false;
            RebirthNpcProgressionService.ImportRecords(new RebirthNpcProgressionRecord[0],new Guid[0],true);
            RebirthNpcProgressionProjectionRevisionService.Reset();loaded=false;boundPath=null;boundWorld=null;originalBinding=null;lastError=string.Empty;return true;
        }
    }
    public static void Reset(bool save){if(!TryReset(save))throw new InvalidOperationException("Progression reset refused; unresolved state retained.");}
    public static string ExportTo(string path){if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("Export path is required.");Write(path,RebirthNpcProgressionService.ExportRecords(),RebirthNpcProgressionService.ExportReplayIds());return path;}
    public static int ImportFrom(string path,bool replace){if(!IsServer())throw new InvalidOperationException("Import requires server authority.");EnsureLoaded();ProgressionFileData data=Read(path);RebirthNpcProgressionService.ImportRecords(data.Records,data.ReplayIds,replace);MarkDirty();return data.Records.Count;}
    public static string GetReport(){return "[REBIRTH NPC Progression Persistence] loaded="+loaded+" dirty="+dirty+" loads="+loads+" saves="+saves+" repairs="+repairs+" error="+(string.IsNullOrEmpty(lastError)?"none":lastError);}
    private static string GetPath(){return boundPath??CurrentPath();}
    private static string CurrentPath(){string d=GameIO.GetSaveGameDir();return string.IsNullOrEmpty(d)?string.Empty:Path.Combine(d,"RebirthNpcProgression.xml");}
    private sealed class ProgressionFileData{public readonly List<RebirthNpcProgressionRecord> Records=new List<RebirthNpcProgressionRecord>();public readonly List<Guid> ReplayIds=new List<Guid>();}
    private static void Write(string path,RebirthNpcProgressionRecord[] records,Guid[] replayIds)
    {
        using(var stream=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None))
        using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Indent=true,Encoding=new System.Text.UTF8Encoding(false)}))
        {WriteContent(writer,records,replayIds);writer.Flush();stream.Flush(true);}
    }
    private static XmlDocument SnapshotDocument(RebirthNpcProgressionRecord[] records,Guid[] replayIds)
    {
        var document=new XmlDocument{XmlResolver=null};
        using(var stream=new MemoryStream())
        {
            using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new System.Text.UTF8Encoding(false),CloseOutput=false}))WriteContent(writer,records,replayIds);
            stream.Position=0;document.Load(stream);
        }
        return document;
    }
    private static void WriteContent(XmlWriter w,RebirthNpcProgressionRecord[] records,Guid[] replayIds)
    {w.WriteStartDocument();w.WriteStartElement("rebirthNpcProgression");w.WriteAttributeString("schema",SchemaVersion.ToString(CultureInfo.InvariantCulture));foreach(var r in records){w.WriteStartElement("npc");w.WriteAttributeString("id",r.NpcId.ToString());w.WriteAttributeString("revision",r.ComponentRevision.ToString(CultureInfo.InvariantCulture));foreach(var e in r.Entries.Values){w.WriteStartElement("entry");w.WriteAttributeString("id",e.ProgressionId);w.WriteAttributeString("xp",e.CumulativeXp.ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("level",e.CachedLevel.ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("curve",e.AppliedCurveVersion.ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("revision",e.EntryRevision.ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("lastTicks",e.LastAwardUtcTicks.ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("lastAward",e.LastAwardId.ToString("N"));w.WriteEndElement();}w.WriteEndElement();}foreach(Guid id in replayIds??new Guid[0]){if(id==Guid.Empty)continue;w.WriteStartElement("replay");w.WriteAttributeString("id",id.ToString("N"));w.WriteEndElement();}w.WriteEndElement();w.WriteEndDocument();}
    private static ProgressionFileData Read(string path)
    {
        return ReadDocument(LoadPrimary(path));
    }
    private static XmlDocument LoadPrimary(string path)
    {
        var doc=new XmlDocument{XmlResolver=null};
        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=67108864}))doc.Load(reader);
        return doc;
    }
    private static ProgressionFileData ReadDocument(XmlDocument doc)
    {
        var result=new ProgressionFileData();
        var root=doc.DocumentElement;if(root==null||root.Name!="rebirthNpcProgression")throw new InvalidDataException("Invalid progression root.");
        int schema=Int(Attr(root,"schema"));if(schema<1||schema>SchemaVersion)throw new InvalidDataException("Unsupported progression schema.");
        var ids=new HashSet<RebirthNpcStableId>();var replay=new HashSet<Guid>();
        foreach(XmlNode n in root.ChildNodes)
        {
            if(!(n is XmlElement))continue;
            if(n.Name=="replay"&&schema>=2){Guid g;if(!Guid.TryParseExact(Attr(n,"id"),"N",out g)||g==Guid.Empty||!replay.Add(g))throw new InvalidDataException("Invalid replay identity.");result.ReplayIds.Add(g);continue;}
            RebirthNpcStableId id;if(n.Name!="npc"||!RebirthNpcStableId.TryParse(Attr(n,"id"),out id)||!ids.Add(id))throw new InvalidDataException("Invalid or duplicate progression NPC.");
            var r=new RebirthNpcProgressionRecord{NpcId=id,ComponentRevision=Long(Attr(n,"revision"))};
            foreach(XmlNode e in n.ChildNodes)
            {
                if(!(e is XmlElement))continue;string key=Attr(e,"id");Guid award=Guid.Empty;string text=Attr(e,"lastAward");
                if(e.Name!="entry"||string.IsNullOrWhiteSpace(key)||r.Entries.ContainsKey(key)||(!string.IsNullOrEmpty(text)&&!Guid.TryParseExact(text,"N",out award)))throw new InvalidDataException("Invalid progression entry.");
                r.Entries.Add(key,new RebirthNpcProgressionEntryRecord{ProgressionId=key,CumulativeXp=Long(Attr(e,"xp")),CachedLevel=Int(Attr(e,"level")),AppliedCurveVersion=Int(Attr(e,"curve")),EntryRevision=Long(Attr(e,"revision")),LastAwardUtcTicks=Long(Attr(e,"lastTicks")),LastAwardId=award});
            }
            result.Records.Add(r);
        }
        return result;
    }
    private static string Attr(XmlNode n,string k){XmlAttribute a=n.Attributes[k];return a==null?string.Empty:a.Value;} private static long Long(string s){long v;if(string.IsNullOrEmpty(s))return 0;if(!long.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out v)||v<0)throw new InvalidDataException("Invalid progression integer.");return v;} private static int Int(string s){int v;if(string.IsNullOrEmpty(s))return 0;if(!int.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out v)||v<0)throw new InvalidDataException("Invalid progression integer.");return v;}
    private static bool IsServer(){ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;return c==null||c.IsServer;}
}



