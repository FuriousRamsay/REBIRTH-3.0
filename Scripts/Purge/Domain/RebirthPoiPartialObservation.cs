using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

// Persisted causal observations are producer assertions, not native completion guesses.
internal sealed class RebirthPoiActorRestoration
{
    public readonly Guid Receipt;
    public readonly int FromEntityId,ToEntityId;
    public RebirthPoiActorRestoration(Guid receipt,int fromEntityId,int toEntityId)
    {if(receipt==Guid.Empty||fromEntityId<0||toEntityId<0||toEntityId<=fromEntityId)throw new ArgumentException("Invalid restoration lineage.");Receipt=receipt;FromEntityId=fromEntityId;ToEntityId=toEntityId;}
}
internal sealed class RebirthPoiRestorationCheckpoint
{
    public readonly long Count;
    public readonly int OriginalEntityId,LastEntityId;
    public readonly string Digest;
    public RebirthPoiRestorationCheckpoint(long count,int originalEntityId,int lastEntityId,string digest)
    {if(count<32||count%32!=0||originalEntityId<0||lastEntityId<=originalEntityId||digest==null||digest.Length!=64||digest.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))throw new ArgumentException("Invalid restoration checkpoint.");Count=count;OriginalEntityId=originalEntityId;LastEntityId=lastEntityId;Digest=digest;}
    public string Canonical {get{return Count.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+OriginalEntityId.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+LastEntityId.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+Digest;} }
}
internal enum RebirthPoiActorTerminalKind { Death=1, NativeDespawn=2 }
internal sealed class RebirthPoiActorObservation
{
    public readonly Guid Token,DeathReceipt;
    public readonly int EntityId,SpawnPoint;
    public readonly string ClassName;
    public readonly ulong DeathTime;
    public readonly IReadOnlyList<RebirthPoiActorRestoration> Restorations;
    public readonly RebirthPoiRestorationCheckpoint Checkpoint;
    public readonly RebirthPoiActorInheritance Inheritance;
    public readonly RebirthPoiActorTerminalKind TerminalKind;
    public bool Dead { get { return DeathReceipt!=Guid.Empty; } }
    public RebirthPoiActorObservation(Guid token,int entityId,string className,int spawnPoint,Guid deathReceipt=default(Guid),ulong deathTime=0,IEnumerable<RebirthPoiActorRestoration> restorations=null,RebirthPoiRestorationCheckpoint checkpoint=null,RebirthPoiActorInheritance inheritance=null,RebirthPoiActorTerminalKind terminalKind=RebirthPoiActorTerminalKind.Death)
    {
        if(token==Guid.Empty||entityId<0||spawnPoint<0||spawnPoint>254||string.IsNullOrWhiteSpace(className)||className.Length>256||className!=className.Trim()||className.Any(char.IsControl)||deathReceipt==Guid.Empty&&deathTime!=0)throw new ArgumentException("Invalid actor observation.");
        if(!Enum.IsDefined(typeof(RebirthPoiActorTerminalKind),terminalKind)||deathReceipt==Guid.Empty&&terminalKind!=RebirthPoiActorTerminalKind.Death)throw new ArgumentException("Invalid actor terminal kind.");
        XmlConvert.VerifyXmlChars(className);
        var lineage=(restorations??Enumerable.Empty<RebirthPoiActorRestoration>()).ToArray();var receipts=new HashSet<Guid>();var ids=new HashSet<int>();
        if(lineage.Length>32)throw new ArgumentException("Restoration lineage budget exhausted.");
        for(int i=0;i<lineage.Length;i++)
        {var entry=lineage[i];if(entry==null||!receipts.Add(entry.Receipt)||!ids.Add(entry.FromEntityId)||i>0&&lineage[i-1].ToEntityId!=entry.FromEntityId)throw new ArgumentException("Invalid restoration chain.");}
        if(lineage.Length>0&&(lineage[lineage.Length-1].ToEntityId!=entityId||ids.Contains(entityId)))throw new ArgumentException("Restoration final actor mismatch.");
        if(checkpoint!=null&&(lineage.Length==0?entityId!=checkpoint.LastEntityId:lineage[0].FromEntityId!=checkpoint.LastEntityId))throw new ArgumentException("Checkpoint predecessor mismatch.");
        TerminalKind=terminalKind;Inheritance=inheritance;Checkpoint=checkpoint;Restorations=Array.AsReadOnly(lineage);Token=token;EntityId=entityId;ClassName=className;SpawnPoint=spawnPoint;DeathReceipt=deathReceipt;DeathTime=deathTime;
    }
    public string LineageCanonical {get{return (Checkpoint==null?"none":Checkpoint.Canonical)+"|"+string.Join(";",Restorations.Select(r=>r.Receipt.ToString("N")+":"+r.FromEntityId.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+r.ToEntityId.ToString(System.Globalization.CultureInfo.InvariantCulture)));} }
    public string CausalDigest {get{string value=Token.ToString("N")+"|"+EntityId.ToString(System.Globalization.CultureInfo.InvariantCulture)+"|"+ClassName.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+ClassName+"|"+SpawnPoint.ToString(System.Globalization.CultureInfo.InvariantCulture)+"|"+LineageCanonical+"|"+(Inheritance==null?"none":Inheritance.Canonical);using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();} }
    public RebirthPoiActorObservation InheritVerifiedReset(long originalEpoch,Guid originalGeneration,Guid reset,Guid survivalReceipt,bool unresolved=false)
    {if(Dead)throw new InvalidOperationException("Dead actor cannot be inherited as alive.");var origin=Inheritance;return new RebirthPoiActorObservation(Token,EntityId,ClassName,SpawnPoint,DeathReceipt,DeathTime,Restorations,Checkpoint,new RebirthPoiActorInheritance(origin==null?originalEpoch:origin.OriginEpoch,origin==null?originalGeneration:origin.OriginGeneration,reset,survivalReceipt,unresolved));}
    // Explicit producer receipt; compact only the exact immutable predecessor.
    public RebirthPoiActorObservation RestoreVerifiedNativeParticipant(int newEntityId,Guid receipt)
    {
        if(Dead)throw new InvalidOperationException("A witnessed dead participant cannot be restored.");
        if(newEntityId<=EntityId)throw new ArgumentException("Restoration must use freshly allocated native successor.");
        var checkpoint=Checkpoint;IEnumerable<RebirthPoiActorRestoration> retained=Restorations;
        if(Restorations.Count==32)
        {
            long count=checkpoint==null?32:checked(checkpoint.Count+32);string digest;
            using(var hash=System.Security.Cryptography.SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(LineageCanonical))).Replace("-","").ToLowerInvariant();
            checkpoint=new RebirthPoiRestorationCheckpoint(count,checkpoint==null?Restorations[0].FromEntityId:checkpoint.OriginalEntityId,EntityId,digest);retained=Enumerable.Empty<RebirthPoiActorRestoration>();
        }
        return new RebirthPoiActorObservation(Token,newEntityId,ClassName,SpawnPoint,DeathReceipt,DeathTime,retained.Concat(new[]{new RebirthPoiActorRestoration(receipt,EntityId,newEntityId)}),checkpoint,Inheritance,TerminalKind);
    }
    public bool IsCausalSuccessorOf(RebirthPoiActorObservation original)
    {
        if(original==null||(Inheritance==null?"none":Inheritance.Canonical)!=(original.Inheritance==null?"none":original.Inheritance.Canonical)||Token!=original.Token||ClassName!=original.ClassName||SpawnPoint!=original.SpawnPoint||original.Dead&&(DeathReceipt!=original.DeathReceipt||DeathTime!=original.DeathTime||TerminalKind!=original.TerminalKind))return false;
        if(EntityId==original.EntityId)return LineageCanonical==original.LineageCanonical;
        if(original.Dead||Dead||Restorations.Count==0)return false;
        try{return original.RestoreVerifiedNativeParticipant(EntityId,Restorations[Restorations.Count-1].Receipt).LineageCanonical==LineageCanonical;}
        catch(ArgumentException){return false;}catch(OverflowException){return false;}
    }
}
internal sealed class RebirthPoiVolumeObservation
{
    public readonly int NativeVolumeId;
    public readonly string Descriptor;
    public readonly Guid Generation;
    public readonly IReadOnlyDictionary<Guid,RebirthPoiActorObservation> Actors;
    public RebirthPoiVolumeObservation(int nativeVolumeId,string descriptor,IEnumerable<RebirthPoiActorObservation> actors,Guid generation=default(Guid))
    {
        if(nativeVolumeId<0||descriptor==null||descriptor.Length!=64||descriptor.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||actors==null)throw new ArgumentException("Invalid volume observation.");
        var map=new Dictionary<Guid,RebirthPoiActorObservation>();var ids=new HashSet<int>();
        foreach(var actor in actors){if(actor==null||map.Count>=8192||map.ContainsKey(actor.Token)||!ids.Add(actor.EntityId))throw new ArgumentException("Duplicate or excessive actor observations.");map.Add(actor.Token,actor);}
        NativeVolumeId=nativeVolumeId;Descriptor=descriptor;Generation=generation;Actors=new ReadOnlyDictionary<Guid,RebirthPoiActorObservation>(map);
    }
}
internal sealed class RebirthPoiPartialObservation
{
    public readonly Guid Generation;
    public readonly long Epoch,Revision;
    public readonly IReadOnlyDictionary<int,RebirthPoiVolumeObservation> Volumes;
    public readonly int ConservativeCharacters;
    public RebirthPoiPartialObservation(Guid generation,long epoch,long revision,IEnumerable<RebirthPoiVolumeObservation> volumes)
    {
        if(generation==Guid.Empty||epoch<0||revision<1||volumes==null)throw new ArgumentException("Invalid partial generation.");
        var map=new Dictionary<int,RebirthPoiVolumeObservation>();var tokens=new HashSet<Guid>();var receipts=new HashSet<Guid>();var ids=new HashSet<int>();long budget=512;int actorCount=0;
        foreach(var volume in volumes)
        {
            if(volume==null||map.Count>=4096||map.ContainsKey(volume.NativeVolumeId))throw new ArgumentException("Duplicate or excessive volumes.");
            map.Add(volume.NativeVolumeId,volume);budget+=256;
            foreach(var actor in volume.Actors.Values)
            { if(!tokens.Add(actor.Token)||!ids.Add(actor.EntityId)||actor.Dead&&!receipts.Add(actor.DeathReceipt)||++actorCount>8192)throw new ArgumentException("Actor alias or budget exceeded.");foreach(var restoration in actor.Restorations)if(!receipts.Add(restoration.Receipt))throw new ArgumentException("Restoration receipt alias.");if(actor.Inheritance!=null&&(actor.Inheritance.OriginEpoch>=epoch||actor.Inheritance.ResetTransaction!=(volume.Generation==Guid.Empty?generation:volume.Generation)||!receipts.Add(actor.Inheritance.SurvivalReceipt)))throw new ArgumentException("Invalid inherited generation/receipt.");budget+=(actor.Inheritance==null?0:256)+400+6L*actor.ClassName.Length+180L*actor.Restorations.Count+(actor.Checkpoint==null?0:256); }
        }
        if(map.Count==0||budget>RebirthPoiClearanceCodec.MaximumCharacters-4096)throw new ArgumentException("Partial observation encoded budget exhausted.");
        Generation=generation;Epoch=epoch;Revision=revision;ConservativeCharacters=(int)budget;Volumes=new ReadOnlyDictionary<int,RebirthPoiVolumeObservation>(map);
    }
    // A successor cannot replace manifest/generation, forget actors or undo a witnessed death.
    public bool IsSuccessorOf(RebirthPoiPartialObservation original)
    {
        if(original==null)return Revision==1;
        if(Generation!=original.Generation||Epoch!=original.Epoch||original.Revision==long.MaxValue||Revision!=original.Revision+1||Volumes.Count!=original.Volumes.Count)return false;
        foreach(var pair in original.Volumes)
        {
            RebirthPoiVolumeObservation volume;
            if(!Volumes.TryGetValue(pair.Key,out volume)||volume.Descriptor!=pair.Value.Descriptor||EffectiveGeneration(volume)!=original.EffectiveGeneration(pair.Value))return false;
            foreach(var old in pair.Value.Actors.Values)
            {
                RebirthPoiActorObservation actor;
                if(!volume.Actors.TryGetValue(old.Token,out actor)||!actor.IsCausalSuccessorOf(old))return false;
            }
        }
        return true;
    }
    public Guid EffectiveGeneration(RebirthPoiVolumeObservation volume){return volume.Generation==Guid.Empty?Generation:volume.Generation;}
    // Caller-owned proof must establish the complete unchanged manifest and unaffected volumes.
    public RebirthPoiPartialObservation RollOverVerifiedReset(RebirthPoiResetPlan plan,RebirthPoiAuthoredResetBindings bindings=null)
    {
        if(plan==null||Epoch==long.MaxValue||Revision==long.MaxValue||bindings!=null&&(bindings.OriginalEpoch!=Epoch||bindings.OriginalPlan.Canonical!=plan.Canonical)||plan.IsAuthored&&bindings==null)return null;
        if(!plan.IsAuthored&&plan.Volumes.Any(id=>!Volumes.ContainsKey(id)))return null;
        try
        {
            var result=new List<RebirthPoiVolumeObservation>();var requiredPrior=new HashSet<Guid>();var mapped=new HashSet<string>(StringComparer.Ordinal);
            foreach(var old in Volumes.Values)
            {
                RebirthPoiAuthoredResetExpectation authored=plan.IsAuthored?plan.Authored.SingleOrDefault(e=>e.Kind==RebirthPoiAuthoredResetKind.Sleeper&&e.Descriptor==old.Descriptor):null;
                bool affected=plan.IsAuthored?authored!=null:plan.Volumes.Contains(old.NativeVolumeId);
                if(!affected){result.Add(new RebirthPoiVolumeObservation(old.NativeVolumeId,old.Descriptor,old.Actors.Values,EffectiveGeneration(old)));continue;}
                string key=plan.IsAuthored?authored.Key:"1:"+old.NativeVolumeId.ToString(System.Globalization.CultureInfo.InvariantCulture);RebirthPoiAuthoredRuntimeBinding runtime=null;
                if(bindings!=null&&(!bindings.Bindings.TryGetValue(key,out runtime)||runtime.Descriptor!=old.Descriptor))return null;
                mapped.Add(key);var inherited=new List<RebirthPoiActorObservation>();
                foreach(var actor in old.Actors.Values)
                {
                    if(actor.Dead)continue;requiredPrior.Add(actor.Token);RebirthPoiResetActorOutcome outcome;
                    if(bindings==null||!bindings.PriorActors.TryGetValue(actor.Token,out outcome)||!outcome.Matches(this,old,actor))return null;
                    if(outcome.Disposition!=RebirthPoiPriorActorDisposition.NativeRemoved)inherited.Add(actor.InheritVerifiedReset(Epoch,EffectiveGeneration(old),plan.Transaction,outcome.Receipt,outcome.Disposition==RebirthPoiPriorActorDisposition.RetainedUnresolved));
                }
                result.Add(new RebirthPoiVolumeObservation(runtime==null?old.NativeVolumeId:runtime.NativeId,old.Descriptor,inherited,plan.Transaction));
            }
            if(bindings!=null&&!requiredPrior.SetEquals(bindings.PriorActors.Keys))return null;
            if(plan.IsAuthored)
                foreach(var expected in plan.Authored.Where(e=>e.Kind==RebirthPoiAuthoredResetKind.Sleeper&&e.Combat&&!mapped.Contains(e.Key)))
                    result.Add(new RebirthPoiVolumeObservation(bindings.Bindings[expected.Key].NativeId,expected.Descriptor,Enumerable.Empty<RebirthPoiActorObservation>(),plan.Transaction));
            return new RebirthPoiPartialObservation(Generation,Epoch+1,Revision+1,result);
        }
        catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}
    }
    public string Canonical {get {return Write(this).ToString(SaveOptions.DisableFormatting);} }
    internal static XElement Write(RebirthPoiPartialObservation value)
    {
        var node=new XElement("observations",new XAttribute("version",5),new XAttribute("generation",value.Generation.ToString("N")),new XAttribute("epoch",value.Epoch),new XAttribute("revision",value.Revision));
        foreach(var volume in value.Volumes.Values.OrderBy(v=>v.NativeVolumeId))
        {
            var child=new XElement("volume",new XAttribute("id",volume.NativeVolumeId),new XAttribute("descriptor",volume.Descriptor),new XAttribute("generation",value.EffectiveGeneration(volume).ToString("N")));
            foreach(var actor in volume.Actors.Values.OrderBy(a=>a.Token))
            {
                var item=new XElement("actor",new XAttribute("token",actor.Token.ToString("N")),new XAttribute("id",actor.EntityId),new XAttribute("class",actor.ClassName),new XAttribute("point",actor.SpawnPoint));
                if(actor.Inheritance!=null)item.Add(actor.Inheritance.Write());
                if(actor.Checkpoint!=null)item.Add(new XElement("checkpoint",new XAttribute("count",actor.Checkpoint.Count),new XAttribute("original",actor.Checkpoint.OriginalEntityId),new XAttribute("last",actor.Checkpoint.LastEntityId),new XAttribute("digest",actor.Checkpoint.Digest)));
                foreach(var restoration in actor.Restorations)item.Add(new XElement("restore",new XAttribute("receipt",restoration.Receipt.ToString("N")),new XAttribute("from",restoration.FromEntityId),new XAttribute("to",restoration.ToEntityId)));
                if(actor.Dead)item.Add(new XElement("death",new XAttribute("receipt",actor.DeathReceipt.ToString("N")),new XAttribute("time",actor.DeathTime),actor.TerminalKind==RebirthPoiActorTerminalKind.NativeDespawn?new XAttribute("kind",2):null));
                child.Add(item);
            }
            node.Add(child);
        }
        return node;
    }
    internal static RebirthPoiPartialObservation Read(XElement node)
    {
        RebirthPoiClearanceCodec.Shape(node,"observations","version,generation,epoch,revision","volume");
        long version=RebirthPoiClearanceCodec.Number(node,"version");if(version!=1&&version!=2&&version!=3&&version!=4&&version!=5)throw new FormatException();
        var volumes=new List<RebirthPoiVolumeObservation>();
        foreach(var volume in node.Elements())
        {
            RebirthPoiClearanceCodec.Shape(volume,"volume",version==1?"id,descriptor":"id,descriptor,generation","actor");var actors=new List<RebirthPoiActorObservation>();
            foreach(var actor in volume.Elements())
            {
                RebirthPoiClearanceCodec.Shape(actor,"actor","token,id,class,point",version==1?"death":version==2?"restore,death":version==3?"checkpoint,restore,death":"inherit,checkpoint,restore,death");var death=RebirthPoiClearanceCodec.Single(actor,"death");Guid receipt=Guid.Empty;ulong time=0;var terminalKind=RebirthPoiActorTerminalKind.Death;
                if(death!=null){RebirthPoiClearanceCodec.Shape(death,"death",version>=5&&death.Attribute("kind")!=null?"receipt,time,kind":"receipt,time","");if(death.Attribute("kind")!=null)terminalKind=(RebirthPoiActorTerminalKind)RebirthPoiClearanceCodec.Int(death,"kind");receipt=RebirthPoiClearanceCodec.Id(death,"receipt");if(!ulong.TryParse(RebirthPoiClearanceCodec.Text(death,"time"),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out time))throw new FormatException();}
                RebirthPoiRestorationCheckpoint checkpoint=null;var checkpointNode=RebirthPoiClearanceCodec.Single(actor,"checkpoint");if(checkpointNode!=null){RebirthPoiClearanceCodec.Shape(checkpointNode,"checkpoint","count,original,last,digest","");checkpoint=new RebirthPoiRestorationCheckpoint(RebirthPoiClearanceCodec.Number(checkpointNode,"count"),RebirthPoiClearanceCodec.Int(checkpointNode,"original"),RebirthPoiClearanceCodec.Int(checkpointNode,"last"),RebirthPoiClearanceCodec.Text(checkpointNode,"digest"));}
                var lineage=new List<RebirthPoiActorRestoration>();foreach(var restoration in actor.Elements("restore")){RebirthPoiClearanceCodec.Shape(restoration,"restore","receipt,from,to","");lineage.Add(new RebirthPoiActorRestoration(RebirthPoiClearanceCodec.Id(restoration,"receipt"),RebirthPoiClearanceCodec.Int(restoration,"from"),RebirthPoiClearanceCodec.Int(restoration,"to")));}
                actors.Add(new RebirthPoiActorObservation(RebirthPoiClearanceCodec.Id(actor,"token"),RebirthPoiClearanceCodec.Int(actor,"id"),RebirthPoiClearanceCodec.Text(actor,"class"),RebirthPoiClearanceCodec.Int(actor,"point"),receipt,time,lineage,checkpoint,RebirthPoiClearanceCodec.Single(actor,"inherit")==null?null:RebirthPoiActorInheritance.Read(RebirthPoiClearanceCodec.Single(actor,"inherit")),terminalKind));
            }
            volumes.Add(new RebirthPoiVolumeObservation(RebirthPoiClearanceCodec.Int(volume,"id"),RebirthPoiClearanceCodec.Text(volume,"descriptor"),actors,version==1?Guid.Empty:RebirthPoiClearanceCodec.Id(volume,"generation")));
        }
        return new RebirthPoiPartialObservation(RebirthPoiClearanceCodec.Id(node,"generation"),RebirthPoiClearanceCodec.Number(node,"epoch"),RebirthPoiClearanceCodec.Number(node,"revision"),volumes);
    }
}

