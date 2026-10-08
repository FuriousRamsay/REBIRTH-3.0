using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal enum RebirthPoiStoreResult { Published, Duplicate, Missing, Corrupt, Conflict, StaleScope, Uncertain, IoFailure }
internal delegate bool RebirthPoiLedgerMutation(RebirthPoiClearanceLedger ledger,out RebirthPoiClearanceLedger successor);

internal sealed class RebirthPoiWorldStore
{
    private readonly object gate=new object();
    private readonly RebirthPoiWorldBinding binding;
    private readonly Action<string> fault;
    private readonly string manifestPath;
    private RebirthPoiWorldSnapshot published;
    private Pending pending;
    private sealed class Pending
    {
        public RebirthPoiWorldSnapshot Original,Candidate;
        public Dictionary<string,string> ShardWrites;
    }
    private RebirthPoiWorldStore(RebirthPoiWorldBinding scope,Action<string> inject)
    { binding=scope; fault=inject; manifestPath=Path.Combine(scope.Directory,"world.xml"); }
    public RebirthPoiWorldSnapshot Published { get { lock(gate) return binding.IsCurrent?published:null; } }
    public bool HasPending { get { lock(gate) return pending!=null; } }
    public static RebirthPoiStoreResult TryOpen(RebirthPoiWorldBinding binding,out RebirthPoiWorldStore store,Action<string> inject=null)
    {
        store=null; if(binding==null || !binding.IsCurrent) return RebirthPoiStoreResult.StaleScope;
        var value=new RebirthPoiWorldStore(binding,inject); store=value;
        lock(value.gate)
        {
            try
            {
                if(!value.SafePath(binding.SaveDirectory) || !binding.IsCurrent) return RebirthPoiStoreResult.StaleScope;
                if(!Directory.Exists(binding.Directory)) Directory.CreateDirectory(binding.Directory);
                if(!value.SafePath(binding.Directory) || !binding.IsCurrent) return RebirthPoiStoreResult.StaleScope;
                using(var lease=value.Lease())
                {
                    if(File.Exists(value.manifestPath))
                    {
                        string text=value.Read(value.manifestPath,RebirthPoiManifestCodec.MaximumCharacters*4L);
                        RebirthPoiWorldSnapshot loaded;
                        if(!value.Load(text,out loaded)) return RebirthPoiStoreResult.Corrupt;
                        if(!binding.IsCurrent) return RebirthPoiStoreResult.StaleScope;
                                                string retainedPath=value.manifestPath+".candidate";
                        if(File.Exists(retainedPath))
                        {
                            string retained=value.Read(retainedPath,RebirthPoiManifestCodec.MaximumCharacters*4L);
                            RebirthPoiWorldSnapshot candidate;
                            if(loaded.Revision==long.MaxValue || !RebirthPoiManifestCodec.IsSuccessorOf(retained,text) ||
                                !value.Load(retained,out candidate) || candidate.Revision!=loaded.Revision+1 || !value.OriginalFinal(text)) return RebirthPoiStoreResult.Corrupt;
                            value.published=loaded; value.pending=new Pending { Original=loaded,Candidate=candidate };
                            return RebirthPoiStoreResult.Uncertain;
                        }
                        if(!value.OriginalFinal(text)) return RebirthPoiStoreResult.Conflict;
                        value.published=loaded; return RebirthPoiStoreResult.Published;
                    }
                    // Never treat a missing final with existing artifacts as a new empty world.
                                        string[] artifacts=Directory.EnumerateFileSystemEntries(binding.Directory).Where(p=>Path.GetFileName(p)!="writer.lock").ToArray();
                    if(artifacts.Length!=0)
                    {
                        string retainedPath=value.manifestPath+".candidate";
                        if(artifacts.Length!=1 || artifacts[0]!=retainedPath) return RebirthPoiStoreResult.Missing;
                        string retained=value.Read(retainedPath,RebirthPoiManifestCodec.MaximumCharacters*4L);
                        RebirthPoiWorldSnapshot candidate;
                        if(!value.Load(retained,out candidate) || candidate.Revision!=0 || candidate.Count!=0 ||
                            retained!=RebirthPoiManifestCodec.Write(binding,0,Enumerable.Empty<RebirthPoiShardReference>())) return RebirthPoiStoreResult.Corrupt;
                        var original=new RebirthPoiWorldSnapshot(binding,0,new Dictionary<string,RebirthPoiClearanceLedger>(),new Dictionary<string,RebirthPoiShardReference>(),null);
                        value.pending=new Pending { Original=original,Candidate=candidate };
                        return RebirthPoiStoreResult.Uncertain;
                    }
                    var empty=new RebirthPoiWorldSnapshot(binding,0,new Dictionary<string,RebirthPoiClearanceLedger>(),new Dictionary<string,RebirthPoiShardReference>(),null);
                    string manifest=RebirthPoiManifestCodec.Write(binding,0,empty.References.Values);
                    var first=new RebirthPoiWorldSnapshot(binding,0,new Dictionary<string,RebirthPoiClearanceLedger>(),new Dictionary<string,RebirthPoiShardReference>(),manifest);
                    value.pending=new Pending { Original=empty,Candidate=first };
                    return value.CommitPending();
                }
            }
            catch { return binding.IsCurrent?RebirthPoiStoreResult.IoFailure:RebirthPoiStoreResult.StaleScope; }
        }
    }
    public RebirthPoiStoreResult TryDiscover(RebirthPoiWorldSnapshot expected,RebirthPoiIdentity identity)
    { return Apply(expected,identity,(RebirthPoiClearanceLedger l,out RebirthPoiClearanceLedger n)=>l.TryDiscover(binding.Scope,binding.WorldId,l.Revision,identity,out n)); }
    public RebirthPoiStoreResult TryClear(RebirthPoiWorldSnapshot expected,RebirthPoiIdentity identity,long epoch,RebirthPoiClearEvidence evidence)
    { return Apply(expected,identity,(RebirthPoiClearanceLedger l,out RebirthPoiClearanceLedger n)=>l.TryClear(binding.Scope,binding.WorldId,l.Revision,identity,epoch,evidence,out n)); }
    public RebirthPoiStoreResult TryBeginReset(RebirthPoiWorldSnapshot expected,RebirthPoiIdentity identity,long epoch,Guid transaction,RebirthPoiResetPlan plan=null)
    { return Apply(expected,identity,(RebirthPoiClearanceLedger l,out RebirthPoiClearanceLedger n)=>l.TryBeginReset(binding.Scope,binding.WorldId,l.Revision,identity,epoch,transaction,out n,plan)); }
    public RebirthPoiStoreResult TryFinishReset(RebirthPoiWorldSnapshot expected,RebirthPoiIdentity identity,long epoch,Guid transaction,RebirthPoiResetDisposition disposition,RebirthPoiPartialObservation retainedObservation=null,RebirthPoiAuthoredResetBindings authoredBindings=null)
    { return Apply(expected,identity,(RebirthPoiClearanceLedger l,out RebirthPoiClearanceLedger n)=>l.TryFinishReset(binding.Scope,binding.WorldId,l.Revision,identity,epoch,transaction,disposition,out n,retainedObservation,authoredBindings)); }
    public RebirthPoiStoreResult TryObserveRepopulation(RebirthPoiWorldSnapshot expected,RebirthPoiIdentity identity,long epoch,RebirthPoiRepopulationEvidence evidence)
    { return Apply(expected,identity,(RebirthPoiClearanceLedger l,out RebirthPoiClearanceLedger n)=>l.TryObserveRepopulation(binding.Scope,binding.WorldId,l.Revision,identity,epoch,evidence,out n)); }
    public RebirthPoiStoreResult TryObservePartial(RebirthPoiWorldSnapshot expected,RebirthPoiIdentity identity,long epoch,RebirthPoiPartialObservation observations)
    { return Apply(expected,identity,(RebirthPoiClearanceLedger l,out RebirthPoiClearanceLedger n)=>l.TryObservePartial(binding.Scope,binding.WorldId,l.Revision,identity,epoch,observations,out n)); }
    private RebirthPoiStoreResult Apply(RebirthPoiWorldSnapshot expected,RebirthPoiIdentity identity,RebirthPoiLedgerMutation mutation)
    {
        lock(gate)
        {
            if(!binding.IsCurrent) return RebirthPoiStoreResult.StaleScope;
            if(pending!=null) return RebirthPoiStoreResult.Uncertain;
            if(expected==null || !ReferenceEquals(expected,published) || !ReferenceEquals(expected.Binding,binding) || identity==null) return RebirthPoiStoreResult.Conflict;
            try
            {
                using(var lease=Lease())
                {
                    if(!OriginalFinal(expected.Manifest)) return RebirthPoiStoreResult.Conflict;
                    if(!binding.IsCurrent) return RebirthPoiStoreResult.StaleScope;
                    string shard;
                    if(!expected.RecordShards.TryGetValue(identity.Key,out shard))
                    {
                        shard=expected.Shards.OrderBy(p=>p.Key,StringComparer.Ordinal).Where(p=>p.Value.Records.Count<RebirthPoiWorldSnapshot.RecordsPerShard).Select(p=>p.Key).FirstOrDefault();
                        if(shard==null) { if(expected.Shards.Count>=RebirthPoiWorldSnapshot.MaximumShards) return RebirthPoiStoreResult.Conflict; shard=Guid.NewGuid().ToString("N"); }
                    }
                    RebirthPoiClearanceLedger original;
                    var values=expected.Shards.TryGetValue(shard,out original)?original.Records.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal):new Dictionary<string,RebirthPoiClearanceRecord>(StringComparer.Ordinal);
                    // Mutate the one original record independently, then repack atomically if its
                    // validated observation growth exhausts the previous shard encoded budget.
                    var one=new Dictionary<string,RebirthPoiClearanceRecord>(StringComparer.Ordinal);
                    RebirthPoiClearanceRecord originalRecord;if(values.TryGetValue(identity.Key,out originalRecord))one.Add(identity.Key,originalRecord);
                    var rebased=new RebirthPoiClearanceLedger(binding.WorldId,binding.Scope,expected.Revision,one);
                    RebirthPoiClearanceLedger successor;
                    if(!mutation(rebased,out successor)||successor==null)return RebirthPoiStoreResult.Conflict;
                    if(!binding.IsCurrent||!ReferenceEquals(expected,published))return RebirthPoiStoreResult.StaleScope;
                    if(ReferenceEquals(rebased,successor))return Witness(expected)?RebirthPoiStoreResult.Duplicate:RebirthPoiStoreResult.Conflict;
                    if(expected.Revision==long.MaxValue||successor.Revision!=expected.Revision+1||successor.Records.Count!=1)return RebirthPoiStoreResult.Conflict;
                    values[identity.Key]=successor.Records[identity.Key];
                    var changed=new Dictionary<string,RebirthPoiClearanceLedger>(StringComparer.Ordinal);
                    try { changed.Add(shard,new RebirthPoiClearanceLedger(binding.WorldId,binding.Scope,successor.Revision,values)); }
                    catch(ArgumentException)
                    {
                        if(values.Count<=1||expected.Shards.Count>=RebirthPoiWorldSnapshot.MaximumShards)return RebirthPoiStoreResult.Conflict;
                        values.Remove(identity.Key);
                        changed.Add(shard,new RebirthPoiClearanceLedger(binding.WorldId,binding.Scope,successor.Revision,values));
                        changed.Add(Guid.NewGuid().ToString("N"),successor);
                    }
                    string transaction=Guid.NewGuid().ToString("N");
                    var references=expected.References.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
                    var shards=expected.Shards.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
                    var writes=new Dictionary<string,string>(StringComparer.Ordinal);
                    foreach(var pair in changed)
                    {
                        if(pair.Value.Records.Count>RebirthPoiWorldSnapshot.RecordsPerShard)return RebirthPoiStoreResult.Conflict;
                        string shardText=RebirthPoiClearanceCodec.Write(pair.Value),file="shard-"+pair.Key+"-"+transaction+".xml";
                        references[pair.Key]=new RebirthPoiShardReference(pair.Key,file,ContentHash(shardText),successor.Revision,pair.Value.Records.Count);
                        shards[pair.Key]=pair.Value;writes.Add(file,shardText);
                    }
                    string manifest=RebirthPoiManifestCodec.Write(binding,successor.Revision,references.Values,ContentHash(expected.Manifest),transaction);
                    var candidate=new RebirthPoiWorldSnapshot(binding,successor.Revision,shards,references,manifest);
                    pending=new Pending { Original=expected,Candidate=candidate,ShardWrites=writes };
                    return CommitPending();
                }
            }
            catch { return pending!=null?RebirthPoiStoreResult.Uncertain:RebirthPoiStoreResult.IoFailure; }
        }
    }
    public RebirthPoiStoreResult TryRetryPending()
    {
        lock(gate)
        {
            if(!binding.IsCurrent) return RebirthPoiStoreResult.StaleScope;
            if(pending==null) return RebirthPoiStoreResult.Conflict;
            try { using(var lease=Lease()) return CommitPending(); }
            catch { return RebirthPoiStoreResult.Uncertain; }
        }
    }
    private RebirthPoiStoreResult CommitPending()
    {
        var retained=pending;
        if(retained==null || !binding.IsCurrent || (published!=null && !ReferenceEquals(published,retained.Original))) return RebirthPoiStoreResult.StaleScope;
        try
        {
            // A successful atomic publish followed by a lost response is witnessed, not repeated.
            if(!OriginalFinal(retained.Candidate.Manifest))
            {
                if(!OriginalFinal(retained.Original.Manifest)) return RebirthPoiStoreResult.Conflict;
                if(!binding.IsCurrent) return RebirthPoiStoreResult.StaleScope;
                if(retained.ShardWrites!=null)
                foreach(var write in retained.ShardWrites.OrderBy(p=>p.Key,StringComparer.Ordinal))
                {
                    Hit("beforeShardWrite");if(!binding.IsCurrent)return RebirthPoiStoreResult.StaleScope;
                    string path=Path.Combine(binding.Directory,write.Key);
                    if(File.Exists(path)){if(Read(path,RebirthPoiClearanceCodec.MaximumCharacters*4L)!=write.Value)return RebirthPoiStoreResult.Conflict;}
                    else WriteNew(path,write.Value);
                    Hit("afterShardWrite");if(!binding.IsCurrent)return RebirthPoiStoreResult.StaleScope;
                }
                Hit("beforeManifestPublish"); if(!binding.IsCurrent || !OriginalFinal(retained.Original.Manifest)) return RebirthPoiStoreResult.StaleScope;
                PublishManifest(retained.Candidate.Manifest,retained.Original.Manifest);
                Hit("afterManifestPublish");
            }
            Hit("beforeWitness");
            if(!binding.IsCurrent || !Witness(retained.Candidate) || !binding.IsCurrent) return RebirthPoiStoreResult.Uncertain;
            Hit("beforeCachePublish");
            if(!binding.IsCurrent || !ReferenceEquals(pending,retained) || (published!=null && !ReferenceEquals(published,retained.Original)) || !OriginalFinal(retained.Candidate.Manifest)) return RebirthPoiStoreResult.StaleScope;
            published=retained.Candidate; pending=null; return RebirthPoiStoreResult.Published;
        }
        catch { return binding.IsCurrent?RebirthPoiStoreResult.Uncertain:RebirthPoiStoreResult.StaleScope; }
    }
    private void Hit(string stage) { if(fault!=null) fault(stage); }
    private FileStream Lease()
    {
        if(!binding.IsCurrent || !SafePath(binding.Directory)) throw new IOException("Stale or unsafe world scope.");
        string path=Path.Combine(binding.Directory,"writer.lock");
        if(!SafePath(path)) throw new IOException("Unsafe lease path.");
        return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    }
    private bool OriginalFinal(string expected)
    {
        if(!binding.IsCurrent || !SafePath(manifestPath)) return false;
        if(expected==null) return !File.Exists(manifestPath);
        return File.Exists(manifestPath) && Read(manifestPath,RebirthPoiManifestCodec.MaximumCharacters*4L)==expected && binding.IsCurrent;
    }
    private bool Witness(RebirthPoiWorldSnapshot expected)
    {
        if(!OriginalFinal(expected.Manifest)) return false;
        RebirthPoiWorldSnapshot loaded;
        return Load(expected.Manifest,out loaded) && loaded.Revision==expected.Revision && OriginalFinal(expected.Manifest);
    }
    private bool Load(string manifest,out RebirthPoiWorldSnapshot snapshot)
    {
        snapshot=null; long revision; Dictionary<string,RebirthPoiShardReference> references;
        if(!binding.IsCurrent || !RebirthPoiManifestCodec.TryRead(manifest,binding,out revision,out references)) return false;
        var shards=new Dictionary<string,RebirthPoiClearanceLedger>(StringComparer.Ordinal);
        foreach(var pair in references)
        {
            if(!binding.IsCurrent) return false;
            string text;
            try { text=Read(Path.Combine(binding.Directory,pair.Value.File),RebirthPoiClearanceCodec.MaximumCharacters*4L); } catch { return false; }
            RebirthPoiClearanceLedger ledger;
            if(ContentHash(text)!=pair.Value.Hash || !RebirthPoiClearanceCodec.TryRead(text,binding.WorldId,binding.Scope,out ledger) ||
                ledger.Revision!=pair.Value.Revision || ledger.Records.Count!=pair.Value.Count) return false;
            shards.Add(pair.Key,ledger);
        }
        snapshot=new RebirthPoiWorldSnapshot(binding,revision,shards,references,manifest); return binding.IsCurrent;
    }
    private string Read(string path,long maximumBytes)
    {
        if(!binding.IsCurrent || !SafePath(path)) throw new IOException("Invalid world file.");
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(stream.Length>maximumBytes) throw new IOException("Oversized world file.");
            using(var reader=new StreamReader(stream,new UTF8Encoding(false,true),false))
            { string text=reader.ReadToEnd(); if(!binding.IsCurrent) throw new IOException("World changed during read."); return text; }
        }
    }
    private void WriteNew(string path,string text)
    {
        if(!binding.IsCurrent || !SafePath(path)) throw new IOException("Invalid candidate path.");
        byte[] bytes=new UTF8Encoding(false,true).GetBytes(text);
        using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { stream.Write(bytes,0,bytes.Length); stream.Flush(true); }
        if(!binding.IsCurrent || Read(path,RebirthPoiClearanceCodec.MaximumCharacters*4L)!=text) throw new IOException("Candidate witness failed.");
    }
    private void PublishManifest(string text,string original)
    {
        // Reuse one original candidate name across uncertain retries. Never delete/repair finals.
        string candidate=manifestPath+".candidate";
        if(File.Exists(candidate)) { if(Read(candidate,RebirthPoiManifestCodec.MaximumCharacters*4L)!=text) throw new IOException("Retained candidate conflict."); }
        else WriteNew(candidate,text);
        Hit("afterManifestCandidateWritten");
        if(!binding.IsCurrent || !OriginalFinal(original)) throw new IOException("World changed before publication.");
        if(File.Exists(manifestPath)) File.Replace(candidate,manifestPath,null);
        else File.Move(candidate,manifestPath);
    }
    private bool SafePath(string path)
    {
        string full=Path.GetFullPath(path),root=binding.SaveDirectory;
        if(full!=root && !full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) return false;
        string current=full;
        while(current!=null && current.Length>=root.Length)
        {
            if((File.Exists(current)||Directory.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) return false;
            if(current==root) break; current=Path.GetDirectoryName(current);
        }
        return true;
    }
    internal static string ContentHash(string text)
    { using(var algorithm=SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(new UTF8Encoding(false,true).GetBytes(text))).Replace("-","").ToLowerInvariant(); }
}


