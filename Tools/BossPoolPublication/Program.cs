using System;using System.IO;using System.Text;using System.Collections.Generic;using System.Threading;using System.Threading.Tasks;
static class Program
{
    static int checks;
    static void Need(bool value,string name){if(!value)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
    sealed class Budget:IRebirthStationSnapshotByteBudget
    {
        public int Calls;public long Live,Peak;public bool Refuse,Throw;public int Maximum=RebirthStationSnapshotEvidence.MaximumSnapshotBytes;
        public int MaximumSnapshotBytes=>Maximum;
        public bool TryReserveAllocation(long n,out IDisposable value){Calls++;value=null;if(Throw)throw new InvalidOperationException("allocation fixture");if(Refuse)return false;Live+=n;Peak=Math.Max(Peak,Live);value=new Lease(this,n);return true;}
        sealed class Lease:IDisposable{Budget b;long n;public Lease(Budget b,long n){this.b=b;this.n=n;}public void Dispose(){if(b!=null){b.Live-=n;b=null;}}}
    }
    sealed class Attempt:IDisposable
    {
        public World World;public Chunk Chunk;public RebirthBossEventInstance Event;public RebirthBossSpawnOriginal Original;
        public RebirthStationSnapshotEvidence.Request Watch;public RebirthBossPublicationCapture Capture;public object Snapshot;public Budget Budget=new Budget();
        public bool Current=true;public int PredicateCalls;
        bool Scope(){PredicateCalls++;return Current;}
        public Attempt(int x=2,bool absent=false,bool death=false,int role=3){
            World=new World();Chunk=new Chunk{X=x};Event=new RebirthBossEventInstance();Original=new RebirthBossSpawnOriginal{Operation=Guid.NewGuid(),EntityId=91,EntityClass=7,WorldTimeBorn=456,Phase=absent||death?(byte)3:(byte)2,Role=death?2:role,NativeProof=absent||death?new string('a',64):"",RemovalInvoking=absent,RemovalX=x,RemovalZ=3,Killed=death};
            GameManager.Instance.World=World;GameIO.Directory="fixture";ThreadManager.Main=true;
            Event.SpawnOriginals.Add(Original);RebirthBossEventRegistry.Events[Event.EventId]=Event;
            Watch=RebirthStationSnapshotEvidence.Watch(World,Chunk,Chunk.X,Chunk.Z,Scope,budget:Budget);
            if(Watch==null)throw new Exception("watch unavailable");
            Capture=new RebirthBossPublicationCapture(Watch,World,Chunk,Event,Original,"fixture",Scope,absent,death);
        }
        public void Serialize(byte[] bytes,object snapshot=null){
            Snapshot=snapshot??new object();var frame=RebirthStationSnapshotEvidence.Begin(Snapshot,Chunk,World,Chunk.X,Chunk.Z,true);
            using(var stream=new MemoryStream(bytes,false)){RebirthStationSnapshotEvidence.Serialized(Chunk,stream);RebirthStationSnapshotEvidence.Finish(frame,stream,null);}
        }
        public void Publish()=>RebirthStationSnapshotEvidence.MarkPublished(Watch,Snapshot,Watch.Generation,"fixture.7rg","positive-readback-double");
        public bool Read()=>Capture.TryRead(World,Chunk,Event,Original,out _);
        public bool Proof()=>Capture.TryProof(World,Chunk,Event,Original,out _);
        public byte[] Bytes(bool entity=true,bool duplicate=false,int? id=null,int? cls=null,ulong? birth=null,byte[] stamp=null)
        {return SnapshotBytes(Chunk.X,Chunk.Z,id??Original.EntityId,cls??Original.EntityClass,birth??Original.WorldTimeBorn,RebirthBossNativePublication.Key(Original),stamp??RebirthBossNativePublication.Stamp(World,Event,Original),entity,duplicate);}
        public void Dispose(){Capture.Dispose();RebirthBossEventRegistry.Events.Remove(Event.EventId);Need(Budget.Live==0,"capture retirement releases shared/retained allocation leases");}
    }
    static void Zeros(BinaryWriter w,int count)=>w.Write(new byte[count]);
    static void Channel(BinaryWriter w,int size){for(int i=0;i<64;i++){w.Write(true);Zeros(w,size);}}
    static byte[] SnapshotBytes(int x,int z,int id,int cls,ulong born,string marker,byte[] stamp,bool entity,bool duplicate){
        using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){
            w.Write(new byte[]{116,116,99,0});w.Write(47);w.Write(x);w.Write(0);w.Write(z);w.Write((ulong)0);Zeros(w,64);Channel(w,1);Zeros(w,256+256+32+256+1536+2);
            w.Write((ushort)1);w.Write(marker);w.Write(ulong.MaxValue);w.Write(false);w.Write((ushort)stamp.Length);w.Write(stamp);Zeros(w,768);Channel(w,1);Channel(w,1);Channel(w,2);Channel(w,6);Channel(w,2);w.Write(false);w.Write(entity?(duplicate?2:1):0);
            if(entity)for(int i=0;i<(duplicate?2:1);i++){w.Write(id);w.Write(cls);w.Write(9);w.Write((byte)EnumSpawnerSource.Dynamic);w.Write(born);}
            w.Flush();return s.ToArray();
        }
    }
    static int Main(string[] args)
    {
        try{
            using(var a=new Attempt()){
                a.Serialize(a.Bytes());a.Publish();Need(RebirthStationSnapshotEvidence.HasPublished(a.Watch),"shared exact publication reached");
                RebirthStationSnapshotEvidence.Invalidate(a.Snapshot);
                Need(!RebirthStationSnapshotEvidence.HasPublished(a.Watch)&&!RebirthStationSnapshotEvidence.TryCopy(a.Watch,out _),"actual shared pooled reset revokes generic evidence");
                if(args.Length>0&&args[0]=="--baseline")Need(RebirthBossNativePublication.BaselinePoll(a.Watch,a.Chunk,a.World,a.Event,a.Original),"next-tick baseline keeps original published proof");
                Need(a.Proof(),"detached image survives pool retirement and passes actual original-span parser");
                int calls=a.Budget.Calls,parsed=RebirthBossNativePublication.Parses;string first=null;
                for(int i=0;i<1000;i++)NeedSilent(a.Capture.TryProof(a.World,a.Chunk,a.Event,a.Original,out first));
                Need(a.Budget.Calls==calls&&RebirthBossNativePublication.Parses==parsed&&first?.Length==64,"1000 checkpoint retries do not clone/hash/reparse again");
                a.Original.Phase=3;a.Original.NativeProof=first;Need(!a.Proof(),"uncheckpointed domain transition cannot reuse original capture");
                a.Original.Phase=2;a.Original.NativeProof="";Need(a.Proof()&&RebirthBossNativePublication.Parses==parsed,"checkpoint rollback reuses exact cached entity proof without parsing again");
                var operation=a.Original.Operation;a.Original.Operation=Guid.NewGuid();Need(!a.Proof(),"wrong original operation refused");a.Original.Operation=operation;
                var born=a.Original.WorldTimeBorn;a.Original.WorldTimeBorn++;Need(!a.Proof(),"wrong captured native birth refused");a.Original.WorldTimeBorn=born;
                var e=a.Event.EventId;a.Event.EventId=Guid.NewGuid();Need(!a.Proof(),"wrong event identity refused");a.Event.EventId=e;
                RebirthBossEventRegistry.Events.Remove(e);Need(!a.Proof(),"retired registry event cannot promote queued proof");RebirthBossEventRegistry.Events[e]=a.Event;
                a.Event.SpawnOriginals.Clear();Need(!a.Proof(),"detached original membership cannot promote queued proof");a.Event.SpawnOriginals.Add(a.Original);
                a.Event.Progression.OwnerStableId="other";Need(!a.Proof(),"changed stable owner refused");a.Event.Progression.OwnerStableId="owner";
                GameIO.Directory="different";Need(!a.Proof(),"wrong save refused");GameIO.Directory="fixture";
                GameManager.Instance.World=new World();Need(!a.Proof(),"retired world refused");GameManager.Instance.World=a.World;
                ThreadManager.Main=false;Need(!a.Proof(),"proof consumption is main-thread only");ThreadManager.Main=true;
                a.Current=false;Need(!a.Proof()&&!a.Capture.ScopeValid,"captured provider/readback scope retirement refuses candidate");a.Current=true;
                a.Watch.Dispose();Need(!a.Proof(),"externally closed request cannot promote queued image");
            }
            using(var a=new Attempt()){
                a.Serialize(a.Bytes());var callback=a.Watch.OnPublished;callback(a.Watch.Generation,new string('0',64));Need(!a.Read(),"callback before positive readback refused");
                a.Publish();int calls=a.Budget.Calls;callback(a.Watch.Generation,new string('0',64));Need(a.Budget.Calls==calls,"duplicate callback does not clone/hash again");
                a.Serialize(a.Bytes(),new object());Need(a.Capture.Superseded&&!a.Proof(),"different pooled carrier with equal counter supersedes old image");
            }
            using(var a=new Attempt()){
                a.Serialize(a.Bytes());a.Publish();object snapshot=a.Snapshot;a.Serialize(a.Bytes(),snapshot);
                Need(a.Capture.Superseded&&!a.Proof(),"same carrier newer generation supersedes old candidate");
            }
            using(var a=new Attempt()){
                a.Serialize(a.Bytes());var callback=a.Watch.OnPublished;a.Watch.OnPublished=(g,d)=>callback(g+1,d);a.Publish();Need(!a.Read(),"wrong publication generation refused");
            }
            using(var a=new Attempt()){
                a.Serialize(a.Bytes());var callback=a.Watch.OnPublished;a.Watch.OnPublished=(g,d)=>callback(g,new string('0',64));a.Publish();Need(!a.Read(),"wrong serialized digest refused");
            }
            using(var a=new Attempt()){
                a.Serialize(a.Bytes());a.Budget.Refuse=true;a.Publish();Need(!a.Read(),"existing request byte-budget refusal preserved");a.Budget.Refuse=false;
            }
            using(var a=new Attempt()){
                a.Serialize(a.Bytes());a.Budget.Throw=true;a.Publish();Need(RebirthStationSnapshotEvidence.HasPublished(a.Watch)&&!a.Read(),"throwing candidate capture leaves shared publication intact and grants no domain proof");a.Budget.Throw=false;
            }
            using(var a=new Attempt()){
                a.Serialize(a.Bytes());a.Budget.Maximum=32*1024*1024;int calls=a.Budget.Calls;a.Publish();Need(!a.Read()&&a.Budget.Calls==calls,"oversize declared budget rejected before any detached clone allocation");
            }
            {
                var a=new Attempt();a.Serialize(a.Bytes());var callback=a.Watch.OnPublished;long generation=a.Watch.Generation;a.Dispose();int calls=a.Budget.Calls;
                callback(generation,new string('a',64));Need(a.Budget.Calls==calls&&!a.Read(),"late callback after closed capture cannot allocate or promote");
            }
            foreach(var kind in new[]{"absent","duplicate","id","class","birth","stamp"}){
                using(var a=new Attempt()){
                    a.Serialize(a.Bytes(entity:kind!="absent",duplicate:kind=="duplicate",id:kind=="id"?92:(int?)null,cls:kind=="class"?8:(int?)null,birth:kind=="birth"?457:(ulong?)null,stamp:kind=="stamp"?new byte[]{1,2} :null));
                    a.Publish();RebirthStationSnapshotEvidence.Invalidate(a.Snapshot);Need(a.Read()&&!a.Proof(),"successful readback alone cannot prove wrong original "+kind);
                }
            }
            using(var a=new Attempt(absent:true)){
                a.Serialize(a.Bytes(entity:false));a.Publish();RebirthStationSnapshotEvidence.Invalidate(a.Snapshot);Need(a.Proof(),"exact original-stamped native absence survives retirement");
                a.Original.RemovalX++;Need(!a.Proof(),"changed removal coordinates refuse cached proof");
            }
            using(var a=new Attempt(absent:true)){
                a.Serialize(a.Bytes());a.Publish();Need(!a.Proof(),"present entity cannot acquire removal proof");
            }
            using(var a=new Attempt(death:true)){
                EntityCreationData.HealthValue=0;a.Serialize(a.Bytes());a.Publish();RebirthStationSnapshotEvidence.Invalidate(a.Snapshot);Need(a.Proof(),"actual callback-qualified dead entity span survives retirement");
                a.Original.Killed=false;Need(!a.Proof(),"cleared death callback cannot reuse cached proof");EntityCreationData.HealthValue=1;
            }
            using(var a=new Attempt(x:-33)){
                a.Serialize(a.Bytes());a.Publish();RebirthStationSnapshotEvidence.Invalidate(a.Snapshot);Need(a.Proof(),"negative chunk identity parsed exactly without local region indexing");
            }
            using(var a=new Attempt()){
                var data=new byte[RebirthStationSnapshotEvidence.MaximumSnapshotBytes];var native=a.Bytes();Buffer.BlockCopy(native,0,data,0,native.Length);
                a.Serialize(data);a.Publish();Need(a.Budget.Peak==2L*data.Length,"8MiB serialized plus retained clone charged at peak16MiB");
                RebirthStationSnapshotEvidence.Invalidate(a.Snapshot);Need(a.Budget.Live==data.Length,"pooled reset frees serializer lease but preserves retained-image lease");
                Need(a.Proof(),"maximum-size original image still requires successful exact span parsing");
            }
            var retained=new List<Attempt>();
            try{
                for(int i=0;i<4;i++){var a=new Attempt(2+i);a.Serialize(a.Bytes());a.Publish();Need(a.Read(),"independent retained quota admits slot"+i);a.Watch.Dispose();retained.Add(a);}
                using(var fifth=new Attempt(10)){fifth.Serialize(fifth.Bytes());int before=fifth.Budget.Calls;fifth.Publish();Need(!fifth.Read()&&fifth.Budget.Calls==before,"independent four-image quota holds after all shared slots were released");}
            }finally{foreach(var a in retained)a.Dispose();}
            using(var a=new Attempt()){a.Serialize(a.Bytes());a.Publish();Need(a.Read(),"retirement returns independent quota for later original");}
            using(var a=new Attempt()){
                a.Serialize(a.Bytes());int calls=a.PredicateCalls;Task.Run(()=>a.Publish()).GetAwaiter().GetResult();
                Need(a.PredicateCalls==calls,"publication callback never invokes game/provider predicate on worker");
                RebirthStationSnapshotEvidence.Invalidate(a.Snapshot);Need(a.Proof(),"writer-thread callback captures data without game predicates");
            }
            foreach(int role in new[]{0,1,2,4})using(var a=new Attempt(role:role)){a.Serialize(a.Bytes());a.Publish();RebirthStationSnapshotEvidence.Invalidate(a.Snapshot);Need(a.Proof(),"exact original publication preserved for role"+role);}
            using(var a=new Attempt())using(var b=new Attempt(4,absent:true)){
                a.Serialize(a.Bytes());a.Publish();b.Serialize(b.Bytes(entity:false));b.Publish();
                RetirementFixture.Own(a.Capture,a.Watch,a.Chunk,b.Capture,b.Watch,b.Chunk);RetirementFixture.Retire();
                Need(a.Budget.Live==0&&b.Budget.Live==0&&a.Watch.Closed&&b.Watch.Closed,"actual Bound+Removing scope retirement releases every retained image/request");
                Need(a.Original.Phase==2&&!b.Original.NativeRemoved&&b.Original.RemovalInvoking,"retirement does not clear native/original obligations or promote receipts");
                RetirementFixture.Retire();Need(a.Budget.Live==0&&b.Budget.Live==0,"repeated scope retirement is idempotent");
            }
            Console.WriteLine("TOTAL "+checks+" PASS; actual capture/shared evidence/parser, ECD/readback/world doubles; native NOT RUN");return 0;
        }catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
    }
    static void NeedSilent(bool value){if(!value)throw new Exception("retry lost proof");}
}
