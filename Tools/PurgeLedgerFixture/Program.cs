using System;
using System.Xml.Linq;

static class Program
{
    static int count;
    static void Check(bool ok,string label) { if(!ok) throw new Exception("FAIL "+label); count++; Console.WriteLine("PASS "+label); }
    static void Reject(Action action,string label) { try { action(); } catch(ArgumentException) { Check(true,label); return; } throw new Exception("FAIL "+label); }
    static void Main()
    {
        object scope=new object(); Guid world=Guid.NewGuid();
        var poi=new RebirthPoiIdentity("House_A",12,44,-19,1,30,20,40,"Forest");
        var alias=new RebirthPoiIdentity("HOUSE_A",12,44,-19,1,30,20,40,"forest");
        var different=new RebirthPoiIdentity("house_a",13,44,-19,1,30,20,40,"forest");
        var otherBiome=new RebirthPoiIdentity("house_a",12,44,-19,1,30,20,40,"desert");
        Check(poi.Equals(alias),"canonical case identity"); Check(!poi.Equals(different),"same prefab different origin");
        Reject(()=>new RebirthPoiIdentity(" bad",0,0,0,0,1,1,1,"forest"),"whitespace identity");
        Reject(()=>new RebirthPoiIdentity("a",0,0,0,4,1,1,1,"forest"),"rotation bounds");
        var empty=new RebirthPoiClearanceLedger(world,scope); RebirthPoiClearanceLedger next;
        Check(!empty.TryDiscover(new object(),world,0,poi,out next),"original world object");
        Check(!empty.TryDiscover(scope,Guid.NewGuid(),0,poi,out next),"original world GUID");
        Check(!empty.TryDiscover(scope,world,1,poi,out next),"stale discovery revision");
        Check(empty.TryDiscover(scope,world,0,poi,out next),"discover"); var discovered=next;
        Check(empty.Records.Count==0 && discovered.Revision==1,"immutable predecessor");
        Check(discovered.TryDiscover(scope,world,0,alias,out next) && ReferenceEquals(next,discovered),"duplicate discovery no write");
        Check(!discovered.TryDiscover(scope,world,1,otherBiome,out next),"identity metadata conflict");
        var proof=new RebirthPoiClearEvidence(Guid.NewGuid(),3,3,15,0,0,false,500);
        Reject(()=>new RebirthPoiClearEvidence(Guid.NewGuid(),0,0,0,0,0,false,500),"zero volume no clearance");
        Reject(()=>new RebirthPoiClearEvidence(Guid.NewGuid(),3,2,15,0,0,false,500),"unopened volume");
        Reject(()=>new RebirthPoiClearEvidence(Guid.NewGuid(),3,3,15,1,0,false,500),"pending spawns");
        Reject(()=>new RebirthPoiClearEvidence(Guid.NewGuid(),3,3,15,0,1,false,500),"live participant");
        Reject(()=>new RebirthPoiClearEvidence(Guid.NewGuid(),3,3,15,0,0,true,500),"active spawning");
        Check(!empty.TryClear(scope,world,0,poi,0,proof,out next),"unknown discovery cannot clear");
        Check(!discovered.TryClear(scope,world,0,poi,0,proof,out next),"clear stale revision");
        Check(discovered.TryClear(scope,world,1,poi,0,proof,out next),"verified clear"); var cleared=next;
        Check(cleared.TryClear(scope,world,1,poi,0,proof,out next) && ReferenceEquals(next,cleared),"clear receipt retry no revision");
        var altered=new RebirthPoiClearEvidence(proof.ProofId,3,3,16,0,0,false,500);
        Check(!cleared.TryClear(scope,world,2,poi,0,altered,out next),"same ID altered evidence rejected");
        Guid reset=Guid.NewGuid();
        Check(cleared.TryBeginReset(scope,world,2,poi,0,reset,out next),"native reset intent"); var pending=next;
        Check(pending.Records[poi.Key].State==RebirthPoiClearanceState.ResetPending && pending.Records[poi.Key].Clear==null,"withhold marker unknown outcome");
        Check(pending.TryBeginReset(scope,world,2,poi,0,reset,out next) && ReferenceEquals(next,pending),"reset original transaction retry");
        Check(!pending.TryBeginReset(scope,world,3,poi,0,Guid.NewGuid(),out next),"competing reset refused");
        Check(!pending.TryClear(scope,world,3,poi,0,proof,out next),"clear blocked during reset");
        Check(!pending.TryFinishReset(scope,world,3,poi,0,reset,RebirthPoiResetDisposition.None,out next),"uncertain reset cannot terminalize");
        Check(!pending.TryFinishReset(new object(),world,3,poi,0,reset,RebirthPoiResetDisposition.Completed,out next),"reset original scope");
        Check(!pending.TryFinishReset(scope,world,2,poi,0,reset,RebirthPoiResetDisposition.Completed,out next),"reset stale revision");
        Check(pending.TryFinishReset(scope,world,3,poi,0,reset,RebirthPoiResetDisposition.NoMutation,out next),"proven no mutation restores"); var restored=next;
        Check(restored.Records[poi.Key].Clear==proof && restored.Records[poi.Key].Epoch==0,"restore original clear evidence");
        Check(restored.TryFinishReset(scope,world,3,poi,0,reset,RebirthPoiResetDisposition.NoMutation,out next) && ReferenceEquals(next,restored),"no mutation terminal receipt retry");
        Check(!restored.TryFinishReset(scope,world,4,poi,0,reset,RebirthPoiResetDisposition.Completed,out next),"conflicting terminal receipt");
        Check(pending.TryFinishReset(scope,world,3,poi,0,reset,RebirthPoiResetDisposition.Completed,out next),"completed reset"); var resetDone=next;
        Check(resetDone.Records[poi.Key].Epoch==1 && resetDone.Records[poi.Key].State==RebirthPoiClearanceState.Discovered,"reset advances epoch clears marker");
        Check(resetDone.TryFinishReset(scope,world,3,poi,0,reset,RebirthPoiResetDisposition.Completed,out next) && ReferenceEquals(next,resetDone),"completed terminal retry");
        Check(!resetDone.TryClear(scope,world,4,poi,0,proof,out next),"old epoch cannot reclear");
        Check(!resetDone.TryBeginReset(scope,world,4,poi,1,reset,out next),"terminal reset ID not reused");
        Check(resetDone.TryClear(scope,world,4,poi,1,proof,out next),"new epoch can clear");
        // Competing successors can be proposed; persistence coordinator must CAS original state.
        Check(discovered.TryDiscover(scope,world,1,different,out next) && next.Revision==2 && cleared.Revision==2,"parallel immutable successor requires coordinator CAS");
        foreach(var source in new[]{empty,discovered,cleared,pending,restored,resetDone})
        {
            string xml=RebirthPoiClearanceCodec.Write(source); RebirthPoiClearanceLedger loaded;
            Check(RebirthPoiClearanceCodec.TryRead(xml,world,scope,out loaded) && RebirthPoiClearanceCodec.Write(loaded)==xml,"roundtrip revision "+source.Revision+" state "+source.Records.Count);
            Check(!RebirthPoiClearanceCodec.TryRead(xml,Guid.NewGuid(),scope,out loaded),"roundtrip wrong world "+source.Revision);
        }
        string valid=RebirthPoiClearanceCodec.Write(pending); RebirthPoiClearanceLedger parsed;
        Check(!RebirthPoiClearanceCodec.TryRead(valid.Replace("version=\"1\"","version=\"2\""),world,scope,out parsed),"unknown schema");
        Check(!RebirthPoiClearanceCodec.TryRead(valid.Replace("state=\"2\"","state=\"99\""),world,scope,out parsed),"unknown state");
        Check(!RebirthPoiClearanceCodec.TryRead(valid.Replace("required=\"3\"","required=\"4\""),world,scope,out parsed),"malformed saved evidence");
        Check(!RebirthPoiClearanceCodec.TryRead(valid.Replace("epoch=\"0\"","epoch=\"-1\""),world,scope,out parsed),"negative epoch");
        Check(!RebirthPoiClearanceCodec.TryRead(valid.Replace("prefab=\"house_a\"","prefab=\"HOUSE_A\""),world,scope,out parsed),"noncanonical saved identity");
        XElement node=XElement.Parse(valid); node.Add(new XElement(node.Element("poi")));
        Check(!RebirthPoiClearanceCodec.TryRead(node.ToString(),world,scope,out parsed),"duplicate instance");
        node=XElement.Parse(valid); node.Element("poi").Add(new XElement(node.Element("poi").Element("reset")));
        Check(!RebirthPoiClearanceCodec.TryRead(node.ToString(),world,scope,out parsed),"duplicate intent");
        Check(!RebirthPoiClearanceCodec.TryRead(valid.Replace("<poi ","<poi unknown=\"1\" "),world,scope,out parsed),"unknown attributes");
        Check(!RebirthPoiClearanceCodec.TryRead("<!DOCTYPE x [<!ENTITY z 'danger'>]>"+valid,world,scope,out parsed),"DTD prohibited");
        Check(!RebirthPoiClearanceCodec.TryRead(new string('x',RebirthPoiClearanceCodec.MaximumCharacters+1),world,scope,out parsed),"payload limit");
        Check(!RebirthPoiClearanceCodec.TryRead("<a><a><a><a><a><a><a><a><a><a/></a></a></a></a></a></a></a></a></a>",world,scope,out parsed),"depth limit");
        Check(!RebirthPoiClearanceCodec.TryRead(valid,world,null,out parsed),"missing runtime scope");
        Check(!RebirthPoiClearanceCodec.TryRead(valid+"<another/>",world,scope,out parsed),"trailing second root");
        Check(!RebirthPoiClearanceCodec.TryRead("<!--outside-->"+valid,world,scope,out parsed),"leading external comment");
        Check(!RebirthPoiClearanceCodec.TryRead(valid+"<!--outside-->",world,scope,out parsed),"trailing external comment");
        Check(!RebirthPoiClearanceCodec.TryRead(valid+"<?outside value?>",world,scope,out parsed),"trailing processing instruction");
        Check(!RebirthPoiClearanceCodec.TryRead(valid+"unknown",world,scope,out parsed),"trailing nonwhitespace");
        Reject(()=>new RebirthPoiIdentity("a"+'\ud800',0,0,0,0,1,1,1,"forest"),"unpaired UTF16 high surrogate");
        Reject(()=>new RebirthPoiIdentity("a"+'\udc00',0,0,0,0,1,1,1,"forest"),"unpaired UTF16 low surrogate");
        var repopulation=new RebirthPoiRepopulationEvidence(Guid.NewGuid(),2,900);
        Reject(()=>new RebirthPoiRepopulationEvidence(Guid.NewGuid(),0,900),"elapsed respawn time not proof");
        Check(!pending.TryObserveRepopulation(scope,world,3,poi,0,repopulation,out next),"uncertain reset remains withheld during repopulation");
        Check(!cleared.TryObserveRepopulation(scope,world,1,poi,0,repopulation,out next),"repopulation stale revision");
        Check(!cleared.TryObserveRepopulation(new object(),world,2,poi,0,repopulation,out next),"repopulation original world guard");
        Check(cleared.TryObserveRepopulation(scope,world,2,poi,0,repopulation,out next),"verified natural repopulation"); var repopulated=next;
        Check(repopulated.Records[poi.Key].Epoch==1 && repopulated.Records[poi.Key].State==RebirthPoiClearanceState.Discovered,"natural repopulation invalidates marker and epoch");
        Check(!repopulated.TryClear(scope,world,3,poi,0,proof,out next),"repopulation blocks stale prior clear");
        Check(repopulated.TryObserveRepopulation(scope,world,2,poi,0,repopulation,out next) && ReferenceEquals(next,repopulated),"repopulation native generation retry idempotent");
        Check(!repopulated.TryObserveRepopulation(scope,world,3,poi,0,new RebirthPoiRepopulationEvidence(repopulation.GenerationId,3,900),out next),"repopulation generation altered witness rejected");
        string repopulatedXml=RebirthPoiClearanceCodec.Write(repopulated);
        Check(RebirthPoiClearanceCodec.TryRead(repopulatedXml,world,scope,out parsed) && RebirthPoiClearanceCodec.Write(parsed)==repopulatedXml,"repopulation receipt persisted");
        Check(parsed.TryObserveRepopulation(scope,world,2,poi,0,repopulation,out next) && ReferenceEquals(next,parsed),"repopulation retry after reload");
        Check(repopulated.TryClear(scope,world,3,poi,1,proof,out next),"reclear new repopulation epoch"); var recleared=next;
        Check(recleared.TryObserveRepopulation(scope,world,2,poi,0,repopulation,out next) && ReferenceEquals(next,recleared) && next.Records[poi.Key].State==RebirthPoiClearanceState.Cleared,"delayed generation retry does not remove reclear");
        Guid secondReset=Guid.NewGuid();
        Check(repopulated.TryBeginReset(scope,world,3,poi,1,secondReset,out next) && next.Records[poi.Key].LastRepopulation.GenerationId==repopulation.GenerationId,"repopulation receipt survives later reset intent");
        string laterResetXml=RebirthPoiClearanceCodec.Write(next);
        Check(RebirthPoiClearanceCodec.TryRead(laterResetXml,world,scope,out parsed),"later reset with repopulation roundtrip");
        var capacity=new System.Collections.Generic.Dictionary<string,RebirthPoiClearanceRecord>(StringComparer.Ordinal);
        int admitted=0;
        for(int i=0;i<5000;i++)
        {
            var candidate=new RebirthPoiIdentity(new string('a',256),i,0,0,0,1,1,1,new string('b',64));
            capacity[candidate.Key]=new RebirthPoiClearanceRecord(candidate,0,i+1,RebirthPoiClearanceState.Discovered,null,Guid.Empty,RebirthPoiClearanceState.Discovered,null,Guid.Empty,RebirthPoiResetDisposition.None);
            try { var bounded=new RebirthPoiClearanceLedger(world,scope,i+1,capacity); admitted++; }
            catch(ArgumentException) { break; }
        }
        Check(admitted<5000 && admitted>0,"large identity admission encoded budget");
        capacity.Remove(new RebirthPoiIdentity(new string('a',256),admitted,0,0,0,1,1,1,new string('b',64)).Key);
        var atCapacity=new RebirthPoiClearanceLedger(world,scope,admitted,capacity);
        Check(RebirthPoiClearanceCodec.Write(atCapacity).Length<=atCapacity.ConservativeEncodedCharacters,"conservative encoded bound covers large identities");
        Check(!atCapacity.TryDiscover(scope,world,admitted,new RebirthPoiIdentity(new string('a',256),admitted,0,0,0,1,1,1,new string('b',64)),out next),"oversized successor refused without predecessor mutation");        var max=new RebirthPoiClearanceLedger(world,scope,long.MaxValue,new System.Collections.Generic.Dictionary<string,RebirthPoiClearanceRecord>());
        Check(!max.TryDiscover(scope,world,long.MaxValue,poi,out next),"revision overflow refused");
        var custodyPoi=new RebirthPoiIdentity("noncombat",99,0,0,0,10,10,10,"forest");var custody=new RebirthPoiClearanceLedger(world,scope);
        Check(custody.TryDiscover(scope,world,0,custodyPoi,out var custodyDiscovered,true)&&custodyDiscovered.Records[custodyPoi.Key].ResetOnly,"reset-only custody is explicit metadata");
        string custodyXml=RebirthPoiClearanceCodec.Write(custodyDiscovered);Check(RebirthPoiClearanceCodec.TryRead(custodyXml,world,scope,out var custodyRead)&&custodyRead.Records[custodyPoi.Key].ResetOnly,"reset-only metadata survives strict cold codec read");
        Check(!custodyRead.TryClear(scope,world,custodyRead.Revision,custodyPoi,0,new RebirthPoiClearEvidence(Guid.NewGuid(),1,1,1,0,0,false,1),out _),"reset-only target cannot accept combat clear proof");
        var custodyReset=Guid.NewGuid();Check(custodyRead.TryBeginReset(scope,world,custodyRead.Revision,custodyPoi,0,custodyReset,out var custodyPending)&&custodyPending.Records[custodyPoi.Key].ResetOnly,"pending reset retains noncombat custody metadata");
        Check(custodyPending.TryFinishReset(scope,world,custodyPending.Revision,custodyPoi,0,custodyReset,RebirthPoiResetDisposition.Completed,out var custodyDone)&&custodyDone.Records[custodyPoi.Key].ResetOnly,"completed reset never turns custody into discoverable combat POI");
        Check(custodyDone.TryDiscover(scope,world,custodyDone.Revision,custodyPoi,out var promoted)&&!promoted.Records[custodyPoi.Key].ResetOnly&&promoted.Records[custodyPoi.Key].Epoch==1,"qualified combat discovery can promote metadata without losing saved generation");
        Check(!custodyPending.TryDiscover(scope,world,custodyPending.Revision,custodyPoi,out _),"pending reset custody cannot be promoted by discovery");
        var malformedCustody=System.Xml.Linq.XElement.Parse(custodyXml);malformedCustody.Element("poi").Element("resetCustody").SetAttributeValue("version",2);Check(!RebirthPoiClearanceCodec.TryRead(malformedCustody.ToString(),world,scope,out _),"unknown custody metadata version refused");
        Check(!RebirthPoiClearanceCodec.Write(promoted).Contains("resetCustody"),"ordinary combat encoding retains original schema without custody extension");
        Console.WriteLine("RESULT "+count+" PASS; actual production domain linked; no native gameplay proof.");
    }
}