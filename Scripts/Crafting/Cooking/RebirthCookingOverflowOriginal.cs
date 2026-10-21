using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Server-owned cancellation overflow. The parent retains its paused paid row
// until every exact original output has a saved native entity publication.
internal static class RebirthCookingOverflowOriginal
{
    private sealed class Budget:IRebirthStationSnapshotByteBudget
    {
        public int MaximumSnapshotBytes=>RebirthStationSnapshotEvidence.MaximumSnapshotBytes;
        public bool TryReserveAllocation(long bytes,out IDisposable reservation)=>RebirthProductiveEarnedObservationBudget.TryRetainDetached(bytes,out reservation);
    }
    private sealed class Runner:IDisposable
    {
        internal Guid Parent,Leg;internal World World;internal EntityItem Entity;
        internal bool Entered;
        internal RebirthCookingSavedDropObservation Observation;internal Func<bool> Current;
        public void Dispose(){Observation?.Dispose();Observation=null;Entity=null;Current=null;World=null;}
    }
    private static readonly Dictionary<Guid,Runner> runners=new Dictionary<Guid,Runner>();
    private static readonly HashSet<Guid> entering=new HashSet<Guid>();
    private static void Release(Guid parent,Guid leg)
    {if(runners.TryGetValue(parent,out var runner)&&runner.Leg==leg){runner.Dispose();runners.Remove(parent);}}
    private static string DirectoryFor(string name)=>Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name);
    private static RebirthCamperTransferJournal Journal(string name)=>new RebirthCamperTransferJournal(DirectoryFor(name));
    private static RebirthCamperTransferJournal.Record Read(string name,Guid id)=>RebirthCamperTransferJournal.ReadExisting(DirectoryFor(name),id);
    private static byte[] Bytes(XElement node)=>Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting));
    private static string Digest(byte[] bytes)=>RebirthStationPublicationRecord.Digest(bytes);
    private static Guid LegId(Guid parent,int index)
    {using(var sha=SHA256.Create())return new Guid(sha.ComputeHash(Encoding.UTF8.GetBytes("cooking.native.overflow.v1:"+parent.ToString("N")+":"+index)).Take(16).ToArray());}
    private static bool Exact(RebirthCamperTransferJournal.Record record,Guid operation,Guid parent,string creation,byte[] bytes)
        =>record!=null&&record.Operation==operation&&record.Vehicle==parent&&record.OwnerCreation==creation&&record.Revision==0&&record.Before.SequenceEqual(bytes)&&record.After.SequenceEqual(bytes);
    private static void Publish(Guid leg,Guid parent,string creation,byte[] original,RebirthCamperTransferJournal.Record accepted,RebirthStationSnapshotEvidence.PublishedSnapshot proof)
    {
        byte[] bytes=Bytes(new XElement("cookingOverflowPublished",new XAttribute("version",1),new XAttribute("leg",leg.ToString("N")),
            new XAttribute("original",Digest(original)),new XAttribute("accepted",Digest(accepted.Before)),new XAttribute("region",proof.Path),
            new XAttribute("payload",proof.PayloadDigest),new XAttribute("x",proof.ChunkX),new XAttribute("z",proof.ChunkZ),new XAttribute("generation",proof.Generation)));
        Journal("CookingOverflowPublished").Prepare(leg,parent,creation,0,bytes,bytes);
    }
    internal static bool Terminal(RebirthCamperTransferJournal.Record parent,EntityPlayer actor,string owner,string creation,ItemStack[] outputs,int deaths,int entity)
    {
        var record=Read("CookingOverflowOriginals",parent.Operation);
        return record?.State==RebirthCamperTransferJournal.Phase.Applied&&Exact(record,parent.Operation,parent.Operation,creation,Bytes(Original(parent,actor,owner,creation,outputs,deaths,entity)));
    }
    internal static void Tick()
    {
        if(runners.Count==0)return;
        foreach(var pair in runners.ToArray())
        {
            bool keep=false;
            try{keep=ReferenceEquals(pair.Value.World,GameManager.Instance?.World)&&pair.Value.Current?.Invoke()==true;}catch{}
            if(!keep){pair.Value.Dispose();runners.Remove(pair.Key);}
        }
    }
    private static XElement Original(RebirthCamperTransferJournal.Record parent,EntityPlayer actor,string owner,string creation,ItemStack[] outputs,int deaths,int entity)
    {
        if(outputs==null||outputs.Length<1||outputs.Length>byte.MaxValue||outputs.Any(item=>item==null||item.IsEmpty()||item.count<1))throw new InvalidDataException();
        long remaining=4L*1024*1024;byte[] image=RemoteResourceTransactions.CaptureOriginalNativeFrame(outputs,ref remaining);
        var node=new XElement("cookingOverflow",new XAttribute("version",1),new XAttribute("parent",parent.Operation.ToString("N")),
            new XAttribute("source",Digest(parent.Before)),new XAttribute("world",actor.world.worldState.Guid),new XAttribute("owner",owner),
            new XAttribute("creation",creation),new XAttribute("deaths",deaths),new XAttribute("entity",entity),new XElement("outputs",Convert.ToBase64String(image)));
        if(Bytes(node).Length>6*1024*1024)throw new InvalidDataException();return node;
    }
    // Applied is positive historical delivery, so death/reconnect may finish
    // source-removal metadata. Fresh native attempts require the original life.
    internal static bool Advance(RebirthCamperTransferJournal.Record parent,EntityPlayer actor,string owner,string creation,ItemStack[] outputs,int originalDeaths,int originalEntity,Func<bool> sourceCurrent)
    {
        if(!ThreadManager.IsMainThread()||parent==null||actor?.world?.IsRemote()!=false||sourceCurrent==null||!entering.Add(parent.Operation))return false;
        IDisposable workspace=null;
        try
        {
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out workspace))return false;
            var enteredSource=sourceCurrent;var enteredWorld=actor.world;string save=GameIO.GetSaveGameDir();
            sourceCurrent=()=>enteredSource()&&ReferenceEquals(actor.world,enteredWorld)&&ReferenceEquals(GameManager.Instance?.World,enteredWorld)&&GameIO.GetSaveGameDir()==save;
            Tick();if(!sourceCurrent())return false;
            var originalJournal=Journal("CookingOverflowOriginals");var original=originalJournal.Read(parent.Operation);
            int deaths=originalDeaths,entity=originalEntity;
            if(deaths<0||entity<0||original==null&&(actor.Died!=deaths||actor.entityId!=entity))return false;
            byte[] bytes=Bytes(Original(parent,actor,owner,creation,outputs,deaths,entity));
            if(original==null)original=originalJournal.Prepare(parent.Operation,parent.Operation,creation,0,bytes,bytes);
            if(!Exact(original,parent.Operation,parent.Operation,creation,bytes)||original.State==RebirthCamperTransferJournal.Phase.Rejected)return false;
            if(original.State==RebirthCamperTransferJournal.Phase.Applied)return sourceCurrent();
            if(original.State==RebirthCamperTransferJournal.Phase.Prepared)
                original=originalJournal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            var legJournal=Journal("CookingOverflowLegs");
            for(int index=0;index<outputs.Length;index++)
            {
                if(!sourceCurrent())return false;
                var leg=LegId(parent.Operation,index);byte[] legBytes=Bytes(new XElement("cookingOverflowLeg",new XAttribute("version",1),
                    new XAttribute("parent",parent.Operation.ToString("N")),new XAttribute("original",Digest(bytes)),new XAttribute("slot",index)));
                var record=legJournal.Read(leg)??legJournal.Prepare(leg,parent.Operation,creation,0,legBytes,legBytes);
                if(!Exact(record,leg,parent.Operation,creation,legBytes)||record.State==RebirthCamperTransferJournal.Phase.Rejected)return false;
                if(record.State==RebirthCamperTransferJournal.Phase.Applied){Release(parent.Operation,leg);continue;}
                var accepted=Read("CookingOverflowAccepted",leg);var publication=Read("CookingOverflowPublished",leg);
                if(accepted!=null&&record.State!=RebirthCamperTransferJournal.Phase.Uncertain)return false;
                if(publication!=null)
                {
                    if(!Published(publication,accepted,leg,parent.Operation,creation,legBytes))return false;
                    var facts=Journal("CookingOverflowPublished");
                    if(publication.State==RebirthCamperTransferJournal.Phase.Prepared)publication=facts.Advance(leg,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
                    if(publication.State==RebirthCamperTransferJournal.Phase.Uncertain)publication=facts.Advance(leg,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
                    if(record.State!=RebirthCamperTransferJournal.Phase.Uncertain||publication.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
                    legJournal.Advance(leg,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);Release(parent.Operation,leg);continue;
                }
                if(accepted!=null&&(!runners.TryGetValue(parent.Operation,out var observedRunner)||observedRunner.Observation==null||!observedRunner.Observation.SnapshotImage(out _,out _)))
                {
                    var retained=Accepted(accepted,leg,parent.Operation,creation,legBytes);
                    if(retained.Element("snapshot")!=null&&RebirthCookingDropSnapshot.TryRead(save,retained.Element("snapshot"),Convert.FromBase64String(retained.Element("native").Value),sourceCurrent,new Budget(),out var recovered))
                    {Publish(leg,parent.Operation,creation,legBytes,accepted,recovered);Release(parent.Operation,leg);return false;}
                }
                if(actor.Died!=deaths||actor.entityId!=entity||!ReferenceEquals(actor.world.GetEntity(entity),actor)||!actor.IsSpawned()||actor.IsDead())return false;
                if(!runners.TryGetValue(parent.Operation,out var runner))
                {
                    if(runners.Count>=4)return false;
                    runner=new Runner{Parent=parent.Operation,Leg=leg,World=actor.world,Current=sourceCurrent};runners.Add(parent.Operation,runner);
                }
                if(runner.Leg!=leg)return false;
                bool Current()=>sourceCurrent()&&actor.Died==deaths&&actor.entityId==entity&&ReferenceEquals(GameManager.Instance?.World,runner.World)&&ReferenceEquals(actor.world,runner.World)&&ReferenceEquals(actor.world.GetEntity(entity),actor)&&actor.IsSpawned()&&!actor.IsDead();
                if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
                {
                    if(runner.Entered||runner.Entity!=null)return false;
                    // One-use native invocation fence is published inside the
                    // real observer after preflight and immediately before call.
                    bool Fence()
                    {
                        if(!Current()||legJournal.Read(leg)?.State!=RebirthCamperTransferJournal.Phase.Prepared)return false;
                        record=legJournal.Advance(leg,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
                        runner.Entered=true;
                        var fence=legJournal.Read(leg);
                        return Exact(fence,leg,parent.Operation,creation,legBytes)&&fence.State==RebirthCamperTransferJournal.Phase.Uncertain&&Current();
                    }
                    if(!RebirthCookingRefundDropObservation.TryDrop(actor,outputs[index],Current,out _,out var observed,Fence))return false;
                    runner.Entity=observed;
                }
                if(runner.Entity==null&&accepted!=null)
                {
                    var node=Accepted(accepted,leg,parent.Operation,creation,legBytes);
                    runner.Entity=actor.world.GetEntity((int)node.Attribute("entity")) as EntityItem;
                    if(runner.Entity?.entityClass!=(int)node.Attribute("class"))return false;
                }
                // An uncertain attempt without an accepted entity is never
                // interpreted as no spawn and never invokes a replacement drop.
                if(runner.Entity==null)return false;
                void Capture(RebirthStationSnapshotEvidence.EntityImage image,string path,byte[] frame)
                {
                    if(!Current()||frame==null)throw new InvalidDataException();
                    if(accepted!=null)
                    {
                        var old=Accepted(accepted,leg,parent.Operation,creation,legBytes);
                        if((int)old.Attribute("entity")!=runner.Entity.entityId||(int)old.Attribute("class")!=runner.Entity.entityClass||!Convert.FromBase64String(old.Element("native").Value).SequenceEqual(frame))throw new InvalidDataException();
                        return; // Preserve the old immutable descriptor; this new save may still positively publish the same entity frame.
                    }
                    byte[] captured=Bytes(new XElement("cookingOverflowAccepted",new XAttribute("version",2),new XAttribute("leg",leg.ToString("N")),
                        new XAttribute("original",Digest(legBytes)),new XAttribute("entity",runner.Entity.entityId),new XAttribute("class",runner.Entity.entityClass),
                        new XElement("native",Convert.ToBase64String(frame)),RebirthCookingDropSnapshot.Describe(save,path,image)));
                    var acceptedJournal=Journal("CookingOverflowAccepted");acceptedJournal.Prepare(leg,parent.Operation,creation,0,captured,captured);
                    accepted=acceptedJournal.Read(leg);
                    if(!Exact(accepted,leg,parent.Operation,creation,captured)||!Current())throw new InvalidDataException();
                }
                if(runner.Observation==null)runner.Observation=RebirthCookingSavedDropObservation.Begin(actor,runner.Entity,outputs[index],Current,new Budget(),Capture);
                if(runner.Observation==null)return false;
                byte[] native=runner.Observation.CopyNativeFrame();if(native==null)return false;
                if(accepted==null)return false;
                var exactAccepted=Accepted(accepted,leg,parent.Operation,creation,legBytes);
                if(!Convert.FromBase64String(exactAccepted.Element("native").Value).SequenceEqual(native))return false;
                if(!runner.Observation.Saved(out var proof)||!Current())return false;
                Publish(leg,parent.Operation,creation,legBytes,accepted,proof);
                runner.Dispose();runners.Remove(parent.Operation);
                return false; // Next poll reconciles the saved fact before another leg.
            }
            if(!sourceCurrent())return false;
            originalJournal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            var terminal=originalJournal.Read(original.Operation);
            return Exact(terminal,original.Operation,parent.Operation,creation,bytes)&&terminal.State==RebirthCamperTransferJournal.Phase.Applied&&sourceCurrent();
        }
        catch(Exception error){Log.Warning("[Cooking] Original overflow retained: "+error.GetType().Name);return false;}
        finally{workspace?.Dispose();entering.Remove(parent.Operation);}
    }
    private static XElement Accepted(RebirthCamperTransferJournal.Record record,Guid leg,Guid parent,string creation,byte[] original)
    {
        var node=XElement.Parse(Encoding.UTF8.GetString(record.Before));
        string frame=node.Element("native")?.Value;byte[] native=frame==null?null:Convert.FromBase64String(frame);
        int? version=(int?)node.Attribute("version");
        if(node.Name!="cookingOverflowAccepted"||node.Attributes().Count()!=5||version!=1&&version!=2||node.Nodes().Count()!=(version==2?2:1)||node.Element("native")==null||node.Element("native").HasAttributes||node.Element("native").HasElements||
            version==2&&!RebirthCookingDropSnapshot.Valid(node.Element("snapshot"),native?.Length??0)||(string)node.Attribute("leg")!=leg.ToString("N")||(string)node.Attribute("original")!=Digest(original)||
            (int)node.Attribute("entity")<0||(int)node.Attribute("class")<0||native==null||native.Length<1||native.Length>1024*1024||Convert.ToBase64String(native)!=frame||
            !Exact(record,leg,parent,creation,Bytes(node))||record.State==RebirthCamperTransferJournal.Phase.Rejected)throw new InvalidDataException();
        return node;
    }
    private static bool Published(RebirthCamperTransferJournal.Record record,RebirthCamperTransferJournal.Record accepted,Guid leg,Guid parent,string creation,byte[] original)
    {
        if(accepted==null)return false;Accepted(accepted,leg,parent,creation,original);
        var node=XElement.Parse(Encoding.UTF8.GetString(record.Before));string path=(string)node.Attribute("region"),payload=(string)node.Attribute("payload");
        string root=Path.GetFullPath(GameIO.GetSaveGameDir()).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
        return node.Name=="cookingOverflowPublished"&&node.Attributes().Count()==9&&!node.Nodes().Any()&&(int?)node.Attribute("version")==1&&
            (string)node.Attribute("leg")==leg.ToString("N")&&(string)node.Attribute("original")==Digest(original)&&(string)node.Attribute("accepted")==Digest(accepted.Before)&&
            !string.IsNullOrEmpty(path)&&Path.GetFullPath(path).StartsWith(root,StringComparison.OrdinalIgnoreCase)&&payload?.Length==64&&payload.All(Uri.IsHexDigit)&&
            (long)node.Attribute("generation")>0&&node.Attribute("x")!=null&&node.Attribute("z")!=null&&Exact(record,leg,parent,creation,Bytes(node))&&record.State!=RebirthCamperTransferJournal.Phase.Rejected;
    }
}
