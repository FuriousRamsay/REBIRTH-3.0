using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
// Native scope adapters only are doubles; all domain/codec/store/filesystem code is production.
class World { public WorldState worldState=new WorldState(); public bool IsRemote()=>false; }
class WorldState { public string Guid=System.Guid.NewGuid().ToString("N").ToUpperInvariant(); }
class GameManager { public static GameManager Instance; public World World; }
class ConnectionManager { public bool IsServer=true; }
class SingletonMonoBehaviour<T> { public static T Instance; }
class ThreadManager { public static bool IsMainThread()=>true; }
class GameIO { public static string Path; public static string GetSaveGameDir()=>Path; }
static class Program
{
    static int checks;
    static void Check(bool value,string label) { if(!value) throw new Exception("FAIL "+label); checks++; Console.WriteLine("PASS "+label); }
    static string Root()=>System.IO.Path.Combine(System.IO.Path.GetTempPath(),"rebirth-purge-fixture-"+Guid.NewGuid().ToString("N"));
    static RebirthPoiWorldBinding Bind(string root,Func<bool> current=null) { Directory.CreateDirectory(root); return new RebirthPoiWorldBinding(new object(),Guid.NewGuid().ToString("N").ToUpperInvariant(),root,current??(()=>true)); }
    static RebirthPoiIdentity Poi(int i)=>new RebirthPoiIdentity(new string('p',256),i,30,0,0,1,1,1,new string('b',64));
    static void Main(string[] args)
    {
        string root=Root(); var binding=Bind(root); RebirthPoiWorldStore store;
        Check(RebirthPoiWorldStore.TryOpen(binding,out store)==RebirthPoiStoreResult.Published,"first save publishes original empty manifest");
        var initial=store.Published;
        Check(initial!=null && initial.Revision==0 && initial.WorldId==binding.WorldId,"native world ID retained");
        Check(store.TryDiscover(initial,Poi(0))==RebirthPoiStoreResult.Published,"discover durable publish");
        var discovered=store.Published;
        Check(store.TryDiscover(initial,Poi(1))==RebirthPoiStoreResult.Conflict,"competing predecessor CAS");
        Check(store.TryDiscover(discovered,Poi(0))==RebirthPoiStoreResult.Duplicate && ReferenceEquals(discovered,store.Published),"duplicate proves final file without revision");
        var proof=new RebirthPoiClearEvidence(Guid.NewGuid(),2,2,4,0,0,false,100);
        Check(store.TryClear(discovered,Poi(0),0,proof)==RebirthPoiStoreResult.Published,"clear durably published");
        Guid transaction=Guid.NewGuid();
        Check(store.TryBeginReset(store.Published,Poi(0),0,transaction)==RebirthPoiStoreResult.Published,"reset intent durable");
        Check(store.Published.TryGet(Poi(0),out var record) && record.State==RebirthPoiClearanceState.ResetPending,"pending reset withholds marker");
        Check(RebirthPoiWorldStore.TryOpen(binding,out var reload)==RebirthPoiStoreResult.Published && reload.Published.TryGet(Poi(0),out record) && record.ResetId==transaction,"original pending reset survives reload");
        Check(reload.TryFinishReset(reload.Published,Poi(0),0,transaction,RebirthPoiResetDisposition.Completed)==RebirthPoiStoreResult.Published,"reset terminal durable");
        Check(store.TryFinishReset(store.Published,Poi(0),0,transaction,RebirthPoiResetDisposition.Completed)==RebirthPoiStoreResult.Conflict,"second store stale final blocked");
        Check(reload.TryClear(reload.Published,Poi(0),0,proof)==RebirthPoiStoreResult.Conflict,"saved epoch stale clear blocked");
        Check(reload.TryClear(reload.Published,Poi(0),1,proof)==RebirthPoiStoreResult.Published,"new epoch clear");
        var repop=new RebirthPoiRepopulationEvidence(Guid.NewGuid(),1,200);
        Check(reload.TryObserveRepopulation(reload.Published,Poi(0),1,repop)==RebirthPoiStoreResult.Published,"natural repopulation durable");
        Check(RebirthPoiWorldStore.TryOpen(binding,out store)==RebirthPoiStoreResult.Published && store.TryObserveRepopulation(store.Published,Poi(0),1,repop)==RebirthPoiStoreResult.Duplicate,"natural generation retry after reload");
        // Larger than the single-domain conservative maximum for these names.
        bool large=true;
        if(args.Contains("quick"))
        {
            // Test setup only: encode the same map through actual production codecs, then
            // exercise production reads/commits. Full mode performs all1200 native-file commits.
            var seeded=store.Published; var references=new System.Collections.Generic.List<RebirthPoiShardReference>();
            seeded.TryGet(Poi(0),out var firstRecord);
            for(int start=0;start<1200;start+=512)
            {
                var values=new System.Collections.Generic.Dictionary<string,RebirthPoiClearanceRecord>(StringComparer.Ordinal);
                for(int i=start;i<Math.Min(start+512,1200);i++)
                {
                    var identity=Poi(i);
                    values[identity.Key]=i==0?firstRecord:new RebirthPoiClearanceRecord(identity,0,seeded.Revision+i,RebirthPoiClearanceState.Discovered,null,Guid.Empty,RebirthPoiClearanceState.Discovered,null,Guid.Empty,RebirthPoiResetDisposition.None);
                }
                long revision=values.Values.Max(r=>r.Revision);
                var ledger=new RebirthPoiClearanceLedger(binding.WorldId,binding.Scope,revision,values);
                string text=RebirthPoiClearanceCodec.Write(ledger),id=Guid.NewGuid().ToString("N"),file="shard-"+id+"-"+Guid.NewGuid().ToString("N")+".xml";
                File.WriteAllText(System.IO.Path.Combine(binding.Directory,file),text,new System.Text.UTF8Encoding(false));
                references.Add(new RebirthPoiShardReference(id,file,RebirthPoiWorldStore.ContentHash(text),revision,values.Count));
            }
            File.WriteAllText(System.IO.Path.Combine(binding.Directory,"world.xml"),RebirthPoiManifestCodec.Write(binding,seeded.Revision+1199,references),new System.Text.UTF8Encoding(false));
            large=RebirthPoiWorldStore.TryOpen(binding,out store)==RebirthPoiStoreResult.Published;
        }
        else for(int i=1;i<1200;i++) if(store.TryDiscover(store.Published,Poi(i))!=RebirthPoiStoreResult.Published) {large=false;break;}
        Check(large && store.Published.Count==1200 && store.Published.Shards.Count==3,"1200 maximum-length POIs across three shards");
        Check(store.Published.References.Values.All(r=>r.Count<=512),"bounded shard counts");
        Check(RebirthPoiWorldStore.TryOpen(binding,out reload)==RebirthPoiStoreResult.Published && reload.Published.Count==1200,"whole map multi-shard reload");
        string unchanged=reload.Published.References.Values.First(r=>r.Count==512).File;
        Check(reload.TryClear(reload.Published,Poi(1199),0,proof)==RebirthPoiStoreResult.Published && reload.Published.References.Values.Any(r=>r.File==unchanged),"unchanged shard publication reused");
        foreach(string stage in new[]{"beforeShardWrite","afterShardWrite","beforeManifestPublish","afterManifestPublish","beforeWitness","beforeCachePublish"})
        {
            string faultRoot=Root(); var faultBinding=Bind(faultRoot); bool enabled=false,hit=false;
            Check(RebirthPoiWorldStore.TryOpen(faultBinding,out var failing,s=>{if(enabled && s==stage && !hit){hit=true;throw new IOException("injected");}})==RebirthPoiStoreResult.Published,"fault setup "+stage);
            enabled=true; var before=failing.Published;
            Check(failing.TryDiscover(before,Poi(0))==RebirthPoiStoreResult.Uncertain && failing.HasPending && ReferenceEquals(failing.Published,before),"retain original pending "+stage);
            string[] files=Directory.GetFiles(faultBinding.Directory,"shard-*.xml");
            Check(failing.TryDiscover(before,Poi(1))==RebirthPoiStoreResult.Uncertain,"new operation blocked "+stage);
            Check(failing.TryRetryPending()==RebirthPoiStoreResult.Published && failing.Published.Count==1 && failing.Published.Revision==1,"original candidate retry "+stage);
            Check(files.All(File.Exists) && Directory.GetFiles(faultBinding.Directory,"shard-*.xml").Length==1,"no replacement transaction files "+stage);
        }
                string restartRoot=Root(); var restartBinding=Bind(restartRoot); bool restartArmed=false;
        Check(RebirthPoiWorldStore.TryOpen(restartBinding,out var interrupted,s=>{if(restartArmed && s=="afterManifestCandidateWritten")throw new IOException("publish interrupted");})==RebirthPoiStoreResult.Published,"restart pending setup");
        restartArmed=true;
        Check(interrupted.TryDiscover(interrupted.Published,Poi(0))==RebirthPoiStoreResult.Uncertain,"durable candidate before commit");
        string retainedCandidate=File.ReadAllText(System.IO.Path.Combine(restartBinding.Directory,"world.xml.candidate"));
        string retainedShard=Directory.GetFiles(restartBinding.Directory,"shard-*.xml").Single();
        Check(RebirthPoiWorldStore.TryOpen(restartBinding,out var resumed)==RebirthPoiStoreResult.Uncertain && resumed.Published.Count==0 && resumed.HasPending,"restart reconstructs original candidate from final preimage");
        Check(resumed.TryRetryPending()==RebirthPoiStoreResult.Published && resumed.Published.Count==1,"restart original transaction commit");
        Check(resumed.Published.Manifest==retainedCandidate && Directory.GetFiles(restartBinding.Directory,"shard-*.xml").Single()==retainedShard,"restart preserves original transaction filename hash and receipt");
        string firstRoot=Root(); var firstBinding=Bind(firstRoot);
        Check(RebirthPoiWorldStore.TryOpen(firstBinding,out var firstPending,s=>{if(s=="afterManifestCandidateWritten")throw new IOException("first interrupted");})==RebirthPoiStoreResult.Uncertain,"interrupted first publication");
        Check(RebirthPoiWorldStore.TryOpen(firstBinding,out resumed)==RebirthPoiStoreResult.Uncertain && resumed.Published==null && resumed.HasPending,"first candidate distinguished from missing corrupted state");
        Check(resumed.TryRetryPending()==RebirthPoiStoreResult.Published && resumed.Published.Count==0,"original first candidate resumed");
        string witnessRoot=Root(); var witnessBinding=Bind(witnessRoot); bool witnessArmed=false;
        Check(RebirthPoiWorldStore.TryOpen(witnessBinding,out var witness,s=>{if(witnessArmed && s=="beforeWitness")File.AppendAllText(Directory.GetFiles(witnessBinding.Directory,"shard-*.xml").Last(),"tamper");})==RebirthPoiStoreResult.Published,"disk witness fault setup");
        witnessArmed=true; var witnessBefore=witness.Published;
        Check(witness.TryDiscover(witnessBefore,Poi(0))==RebirthPoiStoreResult.Uncertain && ReferenceEquals(witness.Published,witnessBefore),"final manifest without exact shard witness cannot publish cache");
        Check(RebirthPoiWorldStore.TryOpen(witnessBinding,out resumed)==RebirthPoiStoreResult.Corrupt,"corrupt referenced shard refuses restart");
        string deletedRoot=Root(); var deletedBinding=Bind(deletedRoot);
        Check(RebirthPoiWorldStore.TryOpen(deletedBinding,out var deleted)==RebirthPoiStoreResult.Published && deleted.TryDiscover(deleted.Published,Poi(0))==RebirthPoiStoreResult.Published,"missing shard setup");
        // Fault injection via move preserves the original bytes for inspection; no deletion.
        string shardToHide=Directory.GetFiles(deletedBinding.Directory,"shard-*.xml").Single(); File.Move(shardToHide,shardToHide+".hidden");
        Check(RebirthPoiWorldStore.TryOpen(deletedBinding,out resumed)==RebirthPoiStoreResult.Corrupt,"missing committed shard not empty first save");
        string staleRoot=Root(); bool live=true; var staleBinding=Bind(staleRoot,()=>live);
        bool arm=false;
        Check(RebirthPoiWorldStore.TryOpen(staleBinding,out var stale,s=>{if(arm && s=="afterShardWrite")live=false;})==RebirthPoiStoreResult.Published,"scope-race setup");
        arm=true; var predecessor=stale.Published;
        Check(stale.TryDiscover(predecessor,Poi(0))==RebirthPoiStoreResult.StaleScope && stale.Published==null && stale.HasPending,"scope change after shard write withholds publication");
        Check(stale.TryRetryPending()==RebirthPoiStoreResult.StaleScope,"stale scope retry no I/O");
        live=true;
        Check(RebirthPoiWorldStore.TryOpen(staleBinding,out var observed)==RebirthPoiStoreResult.Published && observed.Published.Count==0,"old final remains authoritative before commit");
        arm=false; Check(stale.TryRetryPending()==RebirthPoiStoreResult.Published,"original retained candidate can complete only original scope");
        string corruptRoot=Root(); var corruptBinding=Bind(corruptRoot);
        Check(RebirthPoiWorldStore.TryOpen(corruptBinding,out var corrupt)==RebirthPoiStoreResult.Published,"corruption setup");
        string final=System.IO.Path.Combine(corruptBinding.Directory,"world.xml"); File.WriteAllText(final,"<broken>"); File.WriteAllText(final+".bak","<poiWorld version='1'/>");
        Check(RebirthPoiWorldStore.TryOpen(corruptBinding,out observed)==RebirthPoiStoreResult.Corrupt,"malformed final refuses backup substitute");
        Check(File.ReadAllText(final)=="<broken>","malformed final not repaired or deleted");
        string missingRoot=Root(); var missingBinding=Bind(missingRoot); Directory.CreateDirectory(missingBinding.Directory); File.WriteAllText(System.IO.Path.Combine(missingBinding.Directory,"orphan.xml"),"retained");
        Check(RebirthPoiWorldStore.TryOpen(missingBinding,out observed)==RebirthPoiStoreResult.Missing,"missing final with artifacts not first save");
        string wrongRoot=Root(); var wrongBinding=Bind(wrongRoot); Check(RebirthPoiWorldStore.TryOpen(wrongBinding,out var wrong)==RebirthPoiStoreResult.Published,"wrong world setup");
        var replacement=new RebirthPoiWorldBinding(new object(),Guid.NewGuid().ToString("N").ToUpperInvariant(),wrongRoot,()=>true);
        Check(RebirthPoiWorldStore.TryOpen(replacement,out observed)==RebirthPoiStoreResult.Corrupt,"save path different native world GUID rejected");
        string malformed=store.Published.Manifest;
        Check(!RebirthPoiManifestCodec.TryRead(malformed+"<other/>",binding,out _,out _),"manifest whole document envelope");
        Check(!RebirthPoiManifestCodec.TryRead(malformed.Replace("shard-","../shard-"),binding,out _,out _),"manifest path traversal refused");
        Check(!RebirthPoiManifestCodec.TryRead(malformed.Replace("version=\"1\"","version=\"2\""),binding,out _,out _),"manifest unknown schema");
        // Exercise actual native binding code against disclosed native-only doubles.
        GameIO.Path=Root(); Directory.CreateDirectory(GameIO.Path); var world=new World(); GameManager.Instance=new GameManager{World=world}; SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();
        Check(RebirthPoiWorldBinding.TryCapture(world,out var native) && native.NativeWorldId==world.worldState.Guid,"native saved GUID capture");
        world.worldState=new WorldState(); Check(!native.IsCurrent,"native worldState reference changed");
        Console.WriteLine("RESULT "+checks+" PASS; production store/domain/codec and real temp file I/O, native binding methods doubled.");
        Console.WriteLine("Retained fixture roots under "+System.IO.Path.GetTempPath()+"rebirth-purge-fixture-*; no deletion performed.");
    }
}