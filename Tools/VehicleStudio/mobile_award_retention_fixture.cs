// Owner/admission/receipt images and teaching journal are explicit doubles.
// Retention, stable identity and pruner bodies are current production code.
public class RebirthStationGridAdmission
{
 public bool IsMobile=true,IsPublicationAttempted=true;
 public string Job=Guid.NewGuid().ToString("N");
 public bool TryGetMobileAddress(out object address){address=new object();return IsMobile;}
 public XElement Write()=>new XElement("admission",new XAttribute("job",Job));
}
public class FixturePublication
{
 public string Admission;
 public string Receipt=>"camper.craft.v1:"+Admission;
 public XElement Write()=>new XElement("publication",new XAttribute("admission",Admission));
}
public class RebirthWorldProgressionState
{
 public readonly HashSet<string> SkillAwardReceipts=new HashSet<string>(StringComparer.Ordinal);
 public readonly Dictionary<string,RebirthStationGridAdmission> StationPreparations=new Dictionary<string,RebirthStationGridAdmission>();
 public readonly Dictionary<string,FixturePublication> CamperStationPublications=new Dictionary<string,FixturePublication>();
 public readonly Dictionary<string,FixturePublication> CamperStationCompletions=new Dictionary<string,FixturePublication>();
 public readonly Dictionary<string,FixturePublication> CamperStationTrainingPlans=new Dictionary<string,FixturePublication>();
 public RebirthWorldProgressionState(){RebirthCamperStationAwardReceiptRetention.Register(this);}
}
public static class MobileAwardRetentionFixture
{
 static void Check(bool value,string name){if(!value)throw new Exception(name);}
 static void Fill(HashSet<string> receipts){for(int i=0;i<400;i++)receipts.Add("ordinary:"+i.ToString("D4"));}
 public static void Run()
 {
  var owner=new RebirthWorldProgressionState();var admission=new RebirthStationGridAdmission();
  var receipt=RebirthCamperStationTrainingPlan.ReceiptFor(admission);
  owner.StationPreparations.Add(admission.Job,admission);owner.SkillAwardReceipts.Add(receipt);Fill(owner.SkillAwardReceipts);
  RebirthTeachingOutcomeStore.PruneAwardReceipts(owner.SkillAwardReceipts);
  Check(owner.SkillAwardReceipts.Contains(receipt)&&owner.SkillAwardReceipts.Count==257,"active mobile receipt survives ordinary pruning");
  owner.StationPreparations.Clear();
  var publication=new FixturePublication{Admission=receipt.Substring("camper.craft.v1:".Length)};
  owner.CamperStationPublications.Add(admission.Job,publication);Fill(owner.SkillAwardReceipts);
  RebirthTeachingOutcomeStore.PruneAwardReceipts(owner.SkillAwardReceipts);
  Check(owner.SkillAwardReceipts.Contains(receipt),"queued custody retains receipt despite missing preparation");
  owner.CamperStationPublications.Clear();owner.CamperStationCompletions.Add(admission.Job,publication);Fill(owner.SkillAwardReceipts);
  RebirthTeachingOutcomeStore.PruneAwardReceipts(owner.SkillAwardReceipts);
  Check(owner.SkillAwardReceipts.Contains(receipt),"completion custody retains receipt");
  owner.CamperStationCompletions.Clear();owner.CamperStationTrainingPlans.Add(admission.Job,publication);Fill(owner.SkillAwardReceipts);
  RebirthTeachingOutcomeStore.PruneAwardReceipts(owner.SkillAwardReceipts);
  Check(owner.SkillAwardReceipts.Contains(receipt),"frozen training custody retains receipt despite missing parents");
  owner.CamperStationTrainingPlans.Clear();Fill(owner.SkillAwardReceipts);
  RebirthTeachingOutcomeStore.PruneAwardReceipts(owner.SkillAwardReceipts);
  Check(!owner.SkillAwardReceipts.Contains(receipt)&&owner.SkillAwardReceipts.Count==256,"cleared custody returns receipt to bounded history");
  var unknown=new HashSet<string>{receipt};Fill(unknown);RebirthTeachingOutcomeStore.PruneAwardReceipts(unknown);
  Check(unknown.Contains(receipt),"unassociated set cannot prove cleared custody");
  var other=new RebirthWorldProgressionState();other.SkillAwardReceipts.Add(receipt);Fill(other.SkillAwardReceipts);
  RebirthTeachingOutcomeStore.PruneAwardReceipts(other.SkillAwardReceipts);
  Check(!other.SkillAwardReceipts.Contains(receipt),"known other owner does not inherit unrelated custody protection");
  owner.CamperStationCompletions.Add(admission.Job,null);Fill(owner.SkillAwardReceipts);int before=owner.SkillAwardReceipts.Count;
  RebirthTeachingOutcomeStore.PruneAwardReceipts(owner.SkillAwardReceipts);
  Check(owner.SkillAwardReceipts.Count==before,"unknown custody skips pruning conservatively");
  Console.WriteLine("PASS current retention/pruner/receipt identity: active preparation, queued and completed custody, cleared history, unknown sets, owner isolation and corrupt custody. Owner/native admission/teaching journal doubled; no award or gameplay proof.");
 }
}
