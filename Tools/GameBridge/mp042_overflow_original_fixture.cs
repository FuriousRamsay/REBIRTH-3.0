// AUTHORED_NOT_RUN. Full production overflow service; native calls, native save
// observations and journal IO are explicit failure-injectable adapters.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class Log {internal static void Warning(string text){}}
static class GameIO {internal static string GetSaveGameDir()=>Path.Combine(Path.GetTempPath(),"rebirth-overflow-adapter");}
class ItemStack {internal string Rich="rich";internal int count=1;internal bool IsEmpty()=>count==0;internal ItemStack Clone()=>new ItemStack{Rich=Rich,count=count};}
class Entity {internal int entityId;internal World world;}
class EntityItem:Entity {internal int entityClass=9;internal ItemStack Stack;}
class EntityPlayer:Entity {internal int Died=2;internal bool Dead,Spawned=true;internal bool IsDead()=>Dead;internal bool IsSpawned()=>Spawned;}
class World {internal sealed class State {internal string Guid="11111111111111111111111111111111";}internal State worldState=new State();internal bool IsRemote()=>false;internal Dictionary<int,Entity> Entities=new Dictionary<int,Entity>();internal Entity GetEntity(int id)=>Entities.TryGetValue(id,out var e)?e:null;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
static class RemoteResourceTransactions {
    internal static byte[] CaptureOriginalNativeFrame(ItemStack[] outputs,ref long remaining){var bytes=Encoding.UTF8.GetBytes(new XElement("items",outputs.Select(i=>new XElement("item",new XAttribute("rich",i.Rich),new XAttribute("count",i.count)))).ToString(SaveOptions.DisableFormatting));remaining-=bytes.Length;if(remaining<0)throw new InvalidDataException();return bytes;}
}
static class RebirthStationPublicationRecord {internal static string Digest(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}}
public interface IRebirthStationSnapshotByteBudget {int MaximumSnapshotBytes{get;}bool TryReserveAllocation(long n,out IDisposable allocation);}
static class RebirthProductiveEarnedObservationBudget {
    internal static long Live;internal static bool Available=true;
    sealed class Hold:IDisposable {internal long N;public void Dispose(){Live-=N;N=0;}}
    internal static bool TryRetainDetached(long n,out IDisposable allocation){allocation=null;if(!Available)return false;Live+=n;allocation=new Hold{N=n};return true;}
}
static class RebirthStationSnapshotEvidence {
    internal const int MaximumSnapshotBytes=8*1024*1024;
    internal sealed class PublishedSnapshot {internal string Path=System.IO.Path.Combine(GameIO.GetSaveGameDir(),"Region","r.0.0.7rg"),PayloadDigest=new string('A',64);internal int ChunkX,ChunkZ;internal long Generation=1;}
    internal sealed class EntityImage {}
}
static class RebirthCookingDropSnapshot {
    internal static bool Positive;
    internal static XElement Describe(string save,string path,RebirthStationSnapshotEvidence.EntityImage image)=>new XElement("snapshot",new XAttribute("adapter","native-file"));
    internal static bool Valid(XElement node,int bytes)=>node?.Name=="snapshot"&&bytes>0;
    internal static bool TryRead(string save,XElement node,byte[] native,Func<bool> current,IRebirthStationSnapshotByteBudget budget,out RebirthStationSnapshotEvidence.PublishedSnapshot proof){proof=Positive&&current()?new RebirthStationSnapshotEvidence.PublishedSnapshot():null;return proof!=null;}
}
class RebirthCamperTransferJournal {
    internal enum Phase {Prepared,Uncertain,Applied,Rejected}
    internal sealed class Record {internal Guid Operation,Vehicle;internal string OwnerCreation;internal long Revision;internal Phase State;internal byte[] Before,After;}
    internal static Dictionary<string,Dictionary<Guid,Record>> Files=new Dictionary<string,Dictionary<Guid,Record>>();internal static Action<string,Record> AfterPrepare;
    readonly string path;internal RebirthCamperTransferJournal(string p){path=p;if(!Files.ContainsKey(p))Files[p]=new Dictionary<Guid,Record>();}
    internal static Record ReadExisting(string p,Guid id)=>Files.TryGetValue(p,out var rows)&&rows.TryGetValue(id,out var r)?r:null;
    internal Record Read(Guid id)=>ReadExisting(path,id);
    internal Record Prepare(Guid id,Guid parent,string creation,long revision,byte[] before,byte[] after){var old=Read(id);if(old!=null){if(old.Vehicle!=parent||!old.Before.SequenceEqual(before)||!old.After.SequenceEqual(after))throw new InvalidDataException();return old;}var record=new Record{Operation=id,Vehicle=parent,OwnerCreation=creation,Revision=revision,Before=(byte[])before.Clone(),After=(byte[])after.Clone()};Files[path].Add(id,record);AfterPrepare?.Invoke(path,record);return record;}
    internal Record Advance(Guid id,Phase expected,Phase next){var record=Read(id);if(record==null||record.State!=expected)throw new InvalidDataException();record.State=next;return record;}
}
static class RebirthCookingRefundDropObservation {
    internal static int Calls,Mode,Next=100;internal static Action Reenter;
    internal static bool TryDrop(EntityPlayer actor,ItemStack offer,Func<bool> current,out bool attempted,out EntityItem observed,Func<bool> beforeInvoke=null){
        attempted=false;observed=null;if(Mode==1||!current())return false;Reenter?.Invoke();if(beforeInvoke!=null&&!beforeInvoke())return false;if(!current())return false;
        attempted=true;Calls++;var item=new EntityItem{entityId=Next++,world=actor.world,Stack=offer.Clone()};actor.world.Entities[item.entityId]=item;
        if(Mode==2)throw new IOException("native failure after spawn");if(Mode==3)return false;observed=item;return true;
    }
}
sealed class RebirthCookingSavedDropObservation:IDisposable {
    internal static bool Positive,Unavailable,Evidence=true;internal static Action DuringSave;internal static int Begins;
    EntityItem item;Func<bool> current;IDisposable allocation;
    internal static RebirthCookingSavedDropObservation Begin(EntityPlayer owner,EntityItem entity,ItemStack expected,Func<bool> current,IRebirthStationSnapshotByteBudget budget,Action<RebirthStationSnapshotEvidence.EntityImage,string,byte[]> captured=null){if(Unavailable||!current()||!budget.TryReserveAllocation(1024,out var allocation))return null;Begins++;var observation=new RebirthCookingSavedDropObservation{item=entity,current=current,allocation=allocation};try{captured?.Invoke(new RebirthStationSnapshotEvidence.EntityImage(),new RebirthStationSnapshotEvidence.PublishedSnapshot().Path,observation.CopyNativeFrame());return observation;}catch{observation.Dispose();throw;}}
    internal bool SnapshotImage(out RebirthStationSnapshotEvidence.EntityImage image,out string path){image=null;path=null;if(!Evidence||!current())return false;image=new RebirthStationSnapshotEvidence.EntityImage();path=new RebirthStationSnapshotEvidence.PublishedSnapshot().Path;return true;}
    internal byte[] CopyNativeFrame()=>Encoding.UTF8.GetBytes(item.entityId+"|"+item.entityClass+"|"+item.Stack.Rich+"|"+item.Stack.count);
    internal bool Saved(out RebirthStationSnapshotEvidence.PublishedSnapshot proof){DuringSave?.Invoke();proof=Positive&&current()?new RebirthStationSnapshotEvidence.PublishedSnapshot():null;return proof!=null;}
    public void Dispose(){allocation?.Dispose();allocation=null;}
}
// ACTUAL_OVERFLOW
class Program {
    static EntityPlayer actor;static RebirthCamperTransferJournal.Record parent;static ItemStack[] outputs;static bool source;static int assertions;
    static void Need(bool value,string label){assertions++;if(!value)throw new Exception(label);}
    static string PathFor(string name)=>Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name);
    static void Reset(int count=1){
        GameManager.Instance.World=null;RebirthCookingOverflowOriginal.Tick();Need(RebirthProductiveEarnedObservationBudget.Live==0,"previous runner allocations retired");
        RebirthCamperTransferJournal.Files.Clear();RebirthCamperTransferJournal.AfterPrepare=null;
        RebirthCookingRefundDropObservation.Calls=RebirthCookingRefundDropObservation.Mode=0;RebirthCookingRefundDropObservation.Next=100;RebirthCookingRefundDropObservation.Reenter=null;
        RebirthCookingSavedDropObservation.Positive=RebirthCookingSavedDropObservation.Unavailable=false;RebirthCookingSavedDropObservation.DuringSave=null;RebirthCookingSavedDropObservation.Begins=0;RebirthProductiveEarnedObservationBudget.Available=true;
        RebirthCookingDropSnapshot.Positive=false;RebirthCookingSavedDropObservation.Evidence=true;
        var world=new World();actor=new EntityPlayer{world=world,entityId=1};world.Entities[1]=actor;GameManager.Instance.World=world;source=true;
        parent=new RebirthCamperTransferJournal.Record{Operation=Guid.NewGuid(),Vehicle=Guid.NewGuid(),OwnerCreation="creation",Before=new byte[]{1,2,3},After=new byte[]{1,2,3},State=RebirthCamperTransferJournal.Phase.Uncertain};
        outputs=Enumerable.Range(0,count).Select(i=>new ItemStack{Rich="rich-"+i,count=i+2}).ToArray();
    }
    static bool Advance()=>RebirthCookingOverflowOriginal.Advance(parent,actor,"owner","creation",outputs,2,1,()=>source);
    static bool Terminal()=>RebirthCookingOverflowOriginal.Terminal(parent,actor,"owner","creation",outputs,2,1);
    static void Main(){
        Reset(2);Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1&&!Terminal(),"first native attempt retains source");
        Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1,"pending save repeats no drop");
        RebirthCookingSavedDropObservation.Positive=true;Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1,"first saved publication retained for metadata reconciliation");
        Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==2,"saved first leg permits only remaining output");
        Need(Advance()&&Terminal()&&Advance()&&RebirthCookingRefundDropObservation.Calls==2,"all original outputs terminal exactly once");
        actor.Died++;Need(Advance()&&Terminal()&&RebirthCookingRefundDropObservation.Calls==2,"later life retires positive terminal metadata only");
        Need(RebirthProductiveEarnedObservationBudget.Live==0,"completed runner releases all allocations");
        Reset();RebirthCookingRefundDropObservation.Mode=1;Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==0,"preflight unavailable does not invoke");
        RebirthCookingRefundDropObservation.Mode=0;Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1,"never-entered Prepared leg can later enter once");
        for(int mode=2;mode<=3;mode++){
            Reset();RebirthCookingRefundDropObservation.Mode=mode;Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1,"unknown native invocation retained");
            RebirthCookingRefundDropObservation.Mode=0;Need(!Advance()&&!Advance()&&RebirthCookingRefundDropObservation.Calls==1,"throw/suppression cannot authorize replacement spawn");
        }
        Reset();Need(!Advance(),"freeze rich output");outputs[0].count++;Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1,"changed original cannot replace uncertain output");
        Reset();bool nested=true;RebirthCookingRefundDropObservation.Reenter=()=>nested=Advance();Need(!Advance()&&!nested&&RebirthCookingRefundDropObservation.Calls==1,"reentry cannot enter second native invocation");
        Reset();Need(!Advance(),"scope loss fixture admission");source=false;RebirthCookingSavedDropObservation.Positive=true;Need(!Advance()&&!Terminal()&&RebirthCookingRefundDropObservation.Calls==1,"revoked source cannot publish completion");
        Reset(2);RebirthCookingSavedDropObservation.Positive=true;
        RebirthCamperTransferJournal.AfterPrepare=(path,record)=>{if(path==PathFor("CookingOverflowPublished")){RebirthCamperTransferJournal.AfterPrepare=null;throw new IOException("saved fact published but reply lost");}};
        Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1,"publication journal interruption fixture");
        Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==2,"existing saved fact finishes metadata and remaining leg");
        Need(Advance()&&RebirthCookingRefundDropObservation.Calls==2,"fact interruption does not repeat first drop");
        Reset();RebirthCookingSavedDropObservation.Positive=true;Need(!Advance(),"saved fact before death fixture");actor.Died++;
        Need(Advance()&&RebirthCookingRefundDropObservation.Calls==1,"positive saved fact may finish metadata across death");
        Reset(2);RebirthCookingSavedDropObservation.Positive=true;Need(!Advance(),"partial saved outputs fixture");actor.Died++;
        Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1&&!Terminal(),"another life cannot spawn old remaining output");
        Reset();RebirthCookingSavedDropObservation.Positive=true;RebirthCookingSavedDropObservation.DuringSave=()=>actor.Dead=true;
        Need(!Advance()&&!Terminal(),"native observation callback scope loss cannot publish terminal");
        Reset();Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1,"cold accepted entity fixture");
        var retainedWorld=actor.world;GameManager.Instance.World=null;RebirthCookingOverflowOriginal.Tick();GameManager.Instance.World=retainedWorld;
        Need(RebirthProductiveEarnedObservationBudget.Live==0,"cold runner cleanup releases native observation");
        RebirthCookingSavedDropObservation.Positive=true;Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1,"cold exact accepted frame starts observation without another spawn");
        Need(Advance()&&Terminal()&&RebirthCookingRefundDropObservation.Calls==1,"cold exact accepted frame can finish positive publication");
        Reset();Need(!Advance(),"cold changed native frame fixture");retainedWorld=actor.world;GameManager.Instance.World=null;RebirthCookingOverflowOriginal.Tick();GameManager.Instance.World=retainedWorld;
        ((EntityItem)retainedWorld.Entities[100]).Stack.count++;RebirthCookingSavedDropObservation.Positive=true;
        Need(!Advance()&&!Advance()&&!Terminal()&&RebirthCookingRefundDropObservation.Calls==1,"changed cold native frame cannot replace original acceptance or spawn again");
        Reset();RebirthCookingSavedDropObservation.Positive=true;Need(!Advance(),"positive saved fact before disconnect fixture");
        retainedWorld=actor.world;GameManager.Instance.World=null;RebirthCookingOverflowOriginal.Tick();GameManager.Instance.World=retainedWorld;retainedWorld.Entities.Remove(100);actor.Died++;
        Need(Advance()&&Terminal()&&RebirthCookingRefundDropObservation.Calls==1,"positive saved fact survives entity pickup/unload and later life for metadata only");
        Reset();Need(!Advance(),"captured descriptor before lost publication fixture");retainedWorld=actor.world;
        GameManager.Instance.World=null;RebirthCookingOverflowOriginal.Tick();GameManager.Instance.World=retainedWorld;retainedWorld.Entities.Remove(100);actor.Died++;
        RebirthCookingDropSnapshot.Positive=true;
        Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1,"positive cold native file can record publication without live entity or old-life native call");
        Need(Advance()&&Terminal()&&RebirthCookingRefundDropObservation.Calls==1,"cold saved descriptor completes only original metadata");
        Reset();Need(!Advance(),"pooled live observer loss fixture");RebirthCookingSavedDropObservation.Evidence=false;RebirthCookingDropSnapshot.Positive=true;
        Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1,"cold reader can recover lost pooled observer without restarting native drop");
        Need(Advance()&&Terminal(),"recovered pooled native publication reconciles original");
        Reset();RebirthCamperTransferJournal.AfterPrepare=(path,record)=>{if(path==PathFor("CookingOverflowAccepted")){RebirthCamperTransferJournal.AfterPrepare=null;throw new IOException("accepted descriptor persisted before callback failure");}};
        Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1,"accepted capture publication failure retained invocation fence");
        RebirthCookingDropSnapshot.Positive=true;Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1,"cold positive file recovers captured record after callback failure");Need(Advance()&&Terminal(),"capture callback interruption completed as metadata only");
        Reset();Need(!Advance(),"contradictory accepted/fence fixture");RebirthCamperTransferJournal.Files[PathFor("CookingOverflowLegs")].Values.Single().State=RebirthCamperTransferJournal.Phase.Prepared;
        Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1,"accepted native entity plus regressed fence cannot permit another invocation");
        Reset();Need(!Advance(),"legacy accepted record fixture");var accepted=RebirthCamperTransferJournal.Files[PathFor("CookingOverflowAccepted")].Values.Single();
        var legacy=XElement.Parse(Encoding.UTF8.GetString(accepted.Before));legacy.SetAttributeValue("version",1);legacy.Element("snapshot").Remove();accepted.Before=accepted.After=Encoding.UTF8.GetBytes(legacy.ToString(SaveOptions.DisableFormatting));
        retainedWorld=actor.world;GameManager.Instance.World=null;RebirthCookingOverflowOriginal.Tick();GameManager.Instance.World=retainedWorld;RebirthCookingSavedDropObservation.Positive=true;
        Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==1,"legacy accepted frame reobservation must preserve original and avoid native replay");Need(Advance()&&Terminal(),"legacy accepted frame can finish its existing saved observation path");
        Reset();RebirthProductiveEarnedObservationBudget.Available=false;Need(!Advance()&&RebirthCookingRefundDropObservation.Calls==0,"budget refusal cannot enter native action");RebirthProductiveEarnedObservationBudget.Available=true;
        Reset();Console.WriteLine("MP042 actual overflow original assertions="+assertions+"; native persistence and genuine remote topology NOT RUN");
    }
}
