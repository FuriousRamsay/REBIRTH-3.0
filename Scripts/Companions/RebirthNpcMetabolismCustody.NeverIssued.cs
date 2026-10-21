using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthNpcMetabolismCustody
{
    private static long nextNeverIssuedPump;private static int neverIssuedCursor;
    private static bool PositiveNoEffect(string directory,string text)=>File.Exists(Path.Combine(directory,"no-effect.xml"))&&Stage(directory,"no-effect",text)&&Stage(directory,"source-not-entered",text)&&
        !File.Exists(Path.Combine(directory,"source-returned.xml"))&&!File.Exists(Path.Combine(directory,"source-saved.xml"))&&!File.Exists(Path.Combine(directory,"ingestion-intent.xml"));
    private static void PumpColdNeverIssuedBodies()
    {
        if(nativeBodies.Count==0||System.Diagnostics.Stopwatch.GetTimestamp()<nextNeverIssuedPump)return;
        nextNeverIssuedPump=System.Diagnostics.Stopwatch.GetTimestamp()+System.Diagnostics.Stopwatch.Frequency/2;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out var scratch))return;
        using(scratch)try
        {
            string save=Path.GetFullPath(GameIO.GetSaveGameDir()),root=Path.Combine(save,"RebirthData","NpcConsumption");RequirePath(root);
            var directories=Directory.EnumerateDirectories(root).Take(257).ToArray();if(directories.Length==0||directories.Length>256)return;
            neverIssuedCursor%=directories.Length;var selected=directories.Skip(neverIssuedCursor).Take(4).ToArray();neverIssuedCursor=(neverIssuedCursor+selected.Length)%directories.Length;
            foreach(string directory in selected)
            {
                if(File.Exists(Path.Combine(directory,"committed.xml")))continue; // candidate scheduling only; never a release decision.
                var prepared=ReadRecord(Path.Combine(directory,"prepared.xml"));string text=(string)prepared.Element("original");if(!Stage(directory,"prepared",text))continue;var image=ParseOriginal(text);
                if(!RebirthNpcStableId.TryParse((string)image.Attribute("npc"),out var id))continue;
                if(PositiveNoEffect(directory,text)&&Guid.TryParseExact((string)image.Attribute("operation"),"N",out var completedOperation)&&Path.GetFileName(directory)==completedOperation.ToString("N")&&(string)image.Attribute("save")==save)
                {ArchiveCompletedOriginal(new Original{Operation=completedOperation,Directory=directory,Text=text,Save=save,Id=id});continue;}
                var body=nativeBodies.Values.SingleOrDefault(value=>value.Returned&&value.Stable==id&&ReferenceEquals(value.World,GameManager.Instance?.World)&&value.Body?.RebirthRuntimeState?.StableId==value.Stable&&ReferenceEquals(value.World.GetEntity(value.Body.entityId),value.Body));if(body==null)continue;
                if((int?)image.Attribute("version")!=2||(string)image.Attribute("npc")!=body.Stable.ToString()||(string)image.Attribute("save")!=save||(ulong?)image.Attribute("recovery")!=RebirthNpcRecoveryGeneration.RequireCurrent()||
                    !Guid.TryParseExact((string)image.Attribute("operation"),"N",out var operation)||Path.GetFileName(directory)!=operation.ToString("N")||pending.ContainsKey(body.Stable))continue;
                var head=ReadRecord(Path.Combine(directory,"control.xml"));string stage=(string)head.Attribute("stage");
                if(head.Name!="npcConsumptionHead"||head.Attributes().Count()!=2||(int?)head.Attribute("version")!=1||head.Elements().Count()!=1||(string)head.Element("original")!=text)continue;
                if(stage=="source-not-entered"&&Stage(directory,stage,text))
                {var completed=new Original{Operation=operation,Directory=directory,Text=text,Save=save,Id=body.Stable};Record(completed,"no-effect");ArchiveCompletedOriginal(completed);continue;}
                if(stage!="prepared"&&stage!="owner-before-saved"||!Stage(directory,stage,text))continue;
                if(File.Exists(Path.Combine(directory,"source-returned.xml"))||File.Exists(Path.Combine(directory,"source-saved.xml")))continue;
                var native=image.Element("nativeBody");if(native==null||(string)native.Attribute("stable")!=body.Stable.ToString()||(int?)native.Attribute("class")!=body.Body.entityClass)continue;
                ReadOriginalBody(native,body.Stable);
                var before=ReadOriginalSource(image.Element("sourceBefore")?.Element("inventory"),body.Stable,(uint)image.Attribute("revision"));
                string source=RebirthNpcInventoryPersistenceStore.CaptureColdUnissuedConsumptionSource(before);if(source==null)continue;
                var original=new Original{Operation=operation,Directory=directory,Text=text,Save=save,Id=body.Stable};
                string outcomePath=Path.Combine(directory,"source-not-entered.xml");
                if(File.Exists(outcomePath))
                {if(!Stage(directory,"source-not-entered",text))continue;ConvergeExistingStageHead(original,"source-not-entered");}
                else Record(original,"source-not-entered",new XElement("neverIssued",new XAttribute("head",stage),new XElement("loadedNativeBody",Convert.ToBase64String(body.Payload)),new XElement("savedSource",source)));
                // Positive pre-permit control, actual loaded original body and
                // complete saved unchanged source prove zero financial effect.
                Record(original,"no-effect");ArchiveCompletedOriginal(original);
            }
        }
        catch(Exception error){Log.Warning("[REBIRTH NPC] never-issued consumption retained: "+error.Message);}
    }
}
