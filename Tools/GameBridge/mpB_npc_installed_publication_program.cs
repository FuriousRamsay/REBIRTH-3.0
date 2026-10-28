// Actual publication/generation/binding/store code is composed by the runner.
// Native world, domain snapshots and external fence inputs remain adapters.
// Setup constructs a generated installed-generation context; this does not
// exercise administrative backup admission or a native multiplayer session.
static class RebirthNpcRecoveryFaults
{
 internal static string FailStage;
 internal static void PublicationCheckpoint(Guid operation,string path,string stage)
 {if(FailStage==stage){FailStage=null;throw new IOException("fixture interrupted after "+stage);}}
}
static class Program
{
 static void Need(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static string Hash(string path){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
 static void Install(string path,Guid operation)
 {
  Need(RebirthNpcPersistenceFile.TryFenceRecovery(path,operation,out var lease,out var error),"actual original recovery lease: "+error);
  using(lease)
  {
   Need(RebirthNpcRecoveryGeneration.TryAdvance(0,1,operation,out error),"actual generation advance: "+error);
   var scope=RebirthNpcSaveScope.ObserveCurrent();
   new XDocument(new XElement("npcLiveRecovery",new XAttribute("version",1),new XAttribute("phase",2),
    new XAttribute("file",Path.GetFileName(path)),new XAttribute("scope",scope.Fingerprint),
    new XAttribute("operation",operation.ToString("N")),new XAttribute("generation",1))).Save(RebirthNpcPersistenceFile.RecoveryIntentPath(path));
   Need(RebirthNpcRecoveryPublications.TryInitialize(path,operation,1,Hash(path),out error),"actual initialized publication receipt: "+error);
   Need(RebirthNpcRecoveryPublications.TryRegisterInstalled(path,operation,1,out error),"actual installed registration: "+error);
  }
 }
 static ulong ReadLatest(string path,Guid operation,out XmlDocument document)
 {
  Need(RebirthNpcPersistenceFile.TryFenceRecovery(path,operation,out var lease,out var error),"actual readback recovery lease: "+error);
  using(lease)
  {
   Need(RebirthNpcRecoveryPublications.TryResolveLatest(path,operation,1,out document,out var sequence,out var hash,out error),"actual latest publication readback: "+error);
   Need(hash==Hash(path),"receipt hash matches actual primary");return sequence;
  }
 }
 static void Exercise(string parent,bool advanced)
 {
  GameIO.Root=Path.Combine(parent,advanced?"installed-advanced":"installed-progression");Directory.CreateDirectory(GameIO.Root);
  GameManager.Instance.World=new World();var originalWorld=GameManager.Instance.World;var id=new RebirthNpcStableId(1,2);
  string path=Path.Combine(GameIO.Root,advanced?"RebirthNpcAdvancedProgression.xml":"RebirthNpcProgression.xml");
  Action dirty;Func<bool> flush;Action mutate;Func<XmlDocument,bool> matches;
  if(advanced)
  {
   RebirthNpcAdvancedProgressionPersistenceStore.EnsureLoaded();
   var record=new RebirthNpcAdvancedProgressionRecord{NpcId=id};record.Certifications.Add("cert.farming.journeyman");
   RebirthNpcAdvancedProgressionService.Records=new[]{record};dirty=RebirthNpcAdvancedProgressionPersistenceStore.MarkDirty;flush=RebirthNpcAdvancedProgressionPersistenceStore.TryFlush;mutate=()=>record.Revision++;matches=doc=>doc.SelectNodes("/*/npc").Count==1&&((XmlElement)doc.SelectSingleNode("/*/npc")).GetAttribute("id")==id.ToString()&&((XmlElement)doc.SelectSingleNode("/*/npc")).GetAttribute("revision")==record.Revision.ToString(CultureInfo.InvariantCulture)&&record.Certifications.SetEquals(doc.SelectNodes("/*/npc/cert").Cast<XmlElement>().Select(node=>node.GetAttribute("id")));
  }
  else
  {
   RebirthNpcProgressionPersistenceStore.EnsureLoaded();
   var record=new RebirthNpcProgressionRecord{NpcId=id};var entry=new RebirthNpcProgressionEntryRecord{ProgressionId="profession.general",CumulativeXp=50};record.Entries.Add(entry.ProgressionId,entry);
   RebirthNpcProgressionService.Records=new[]{record};dirty=RebirthNpcProgressionPersistenceStore.MarkDirty;flush=RebirthNpcProgressionPersistenceStore.TryFlush;mutate=()=>entry.CumulativeXp+=5;matches=doc=>doc.SelectNodes("/*/npc/entry").Count==1&&((XmlElement)doc.SelectSingleNode("/*/npc")).GetAttribute("id")==id.ToString()&&((XmlElement)doc.SelectSingleNode("/*/npc/entry")).GetAttribute("id")==entry.ProgressionId&&((XmlElement)doc.SelectSingleNode("/*/npc/entry")).GetAttribute("xp")==entry.CumulativeXp.ToString(CultureInfo.InvariantCulture);
  }
  dirty();Need(flush(),"ordinary typed store initial flush");Guid operation=Guid.NewGuid();Install(path,operation);
  Need(RebirthNpcRecoveryPublications.IsInstalled(path)&&!RebirthNpcPersistenceFile.IsRecoveryFenced(path),"actual registered generation admits its exact current writer");
  byte[] before=File.ReadAllBytes(path);mutate();dirty();Need(flush(),"typed store saves through actual installed publication");
  Need(!File.ReadAllBytes(path).SequenceEqual(before)&&!File.Exists(path+".tmp"),"installed typed save updates primary without ordinary temporary");
  Need(ReadLatest(path,operation,out var document)==1&&matches(document),"first typed save advances actual receipt and exact typed payload");
  before=File.ReadAllBytes(path);mutate();dirty();RebirthNpcRecoveryFaults.FailStage="save-pending";
  Need(!flush()&&File.ReadAllBytes(path).SequenceEqual(before)&&!File.Exists(path+".tmp"),"interrupted installed save retains dirty state and old primary");
  var pending=XElement.Load(path+".recovery-publications.xml");Need((string)pending.Attribute("pending")=="1","interrupted publication retains pending receipt");
  byte[] pendingReceipt=File.ReadAllBytes(path+".recovery-publications.xml");string pendingHash=(string)pending.Attribute("pendingHash");
  Need(!flush()&&File.ReadAllBytes(path).SequenceEqual(before)&&File.ReadAllBytes(path+".recovery-publications.xml").SequenceEqual(pendingReceipt),"pending publication cannot fall back or replace its receipt");
  Need(ReadLatest(path,operation,out document)==2&&Hash(path)==pendingHash&&matches(document),"actual reconciliation publishes exact pending hash and typed payload");
  Need(flush()&&ReadLatest(path,operation,out document)==3&&matches(document),"retained typed state flushes exact payload with current receipt after reconciliation");
  GameManager.Instance.World=new World();Need(!RebirthNpcRecoveryPublications.IsInstalled(path)&&RebirthNpcPersistenceFile.IsRecoveryFenced(path),"new World cannot reuse installed publication authority");
  GameManager.Instance.World=originalWorld;
  Console.WriteLine("Actual installed publication path complete: "+Path.GetFileName(path));
 }
 static void Main(string[] args)
 {
  if(args.Length!=1||!Directory.Exists(args[0]))throw new Exception("Generated fixture directory required");
  Exercise(args[0],false);Exercise(args[0],true);
  Console.WriteLine("PASS actual typed stores + publication/receipt/generation/reconciliation; native world/domain inputs adapted; administrative admission and topology NOT RUN.");
 }
}
