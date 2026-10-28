using System;
using System.Reflection;
using System.Xml.Linq;

// PREPARED, NOT RUN. Intended for a future disposable-save, target-native
// harness compiled alongside the actual domain sources. No framing/codec
// doubles. Caller supplies a positively saved original None-mode plan before
// root attempt; this fixture neither authors files nor spawns/mutates items.
internal static class MpALogisticsPreparedRecoveryFixture
{
    private static readonly MethodInfo Encode=typeof(RebirthLogisticsNativeRecovery).GetMethod("PreparedXml",BindingFlags.Static|BindingFlags.NonPublic);
    private static readonly MethodInfo Decode=typeof(RebirthLogisticsNativeRecovery).GetMethod("ReadPrepared",BindingFlags.Static|BindingFlags.NonPublic);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static XElement Write(RebirthLogisticsNativeRecovery.Record root,RebirthLogisticsDeathJournal.Record prepared)
        =>(XElement)Encode.Invoke(null,new object[]{root,prepared});
    private static bool Refuses(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsNativeRecovery.Record root,XElement xml)
    {try{Decode.Invoke(null,new object[]{original,root,xml});return false;}catch(TargetInvocationException error){return error.InnerException is System.IO.InvalidDataException;}}
    internal static void Verify(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsNativeRecovery.Record root)
    {
        Check(root.State==0&&root.Prepared!=null,"fixture needs actual durable pure State0 prepared mapping");
        var canonical=Write(root,root.Prepared);
        var copy=(RebirthLogisticsDeathJournal.Record)Decode.Invoke(null,new object[]{original,root,new XElement(canonical)});
        Check(copy.State==0&&copy.BeforeProof==null&&copy.FinalProof==null,"intent cannot invent native publication evidence");
        Check(XNode.DeepEquals(canonical,Write(root,copy)),"complete native original mapping roundtrip");
        var injected=new XElement(canonical);injected.SetAttributeValue("beforeProof",new string('a',64));
        Check(Refuses(original,root,injected),"publication-proof injection into prepared intent refused");
        injected=new XElement(canonical);injected.Add(new XElement("after",canonical.Element("after").Value));
        Check(Refuses(original,root,injected),"duplicate mapping refuses instead of choosing one");
        if(copy.Parcels.Count>0)
        {
            injected=new XElement(canonical);injected.Add(new XElement(injected.Element("parcel")));
            Check(Refuses(original,root,injected),"duplicate parcel identity refuses");
            injected=new XElement(canonical);injected.Element("parcel").SetAttributeValue("x",root.X+2);
            Check(Refuses(original,root,injected),"mapping cannot move outside original bounded neighborhood");
        }
        Check(copy.Spawn==null||!ReferenceEquals(copy.Spawn,root.Prepared.Spawn),"root native frame detached");
        // Actual stale-baseline/no-spawn, native sector limit, cold attempted
        // absence, and source Outcome ordering require the separate native
        // scenario. This codec fixture does not claim those lifecycle proofs.
    }
    internal static void VerifyPaidPostimageConstraints(RebirthLogisticsTransferJournal.Record actualSettledOriginal)
    {
        var method=typeof(RebirthLogisticsNativeDeathReceipt).GetMethod("HasSettledPaidOutcome",BindingFlags.Static|BindingFlags.NonPublic);
        Func<RebirthLogisticsTransferJournal.Record,bool> accepts=value=>(bool)method.Invoke(null,new object[]{value});
        RebirthLogisticsTransferJournal.Record Copy()
        {
            Check(RebirthLogisticsTransferJournal.TryDecode(RebirthLogisticsTransferJournal.Encode(actualSettledOriginal),out var value),"actual original DTO clone");return value;
        }
        var original=Copy();Check(accepts(original),"fixture starts with real settled paid original");
        original.OwnerDebitProof=null;Check(!accepts(original),"saved final inventory cannot invent original payment");
        original=Copy();original.Outcome=0;Check(!accepts(original),"unsettled original cannot terminalize from postimage");
        original=Copy();original.Phase=1;Check(!accepts(original),"source operation must already have paid outcome");
        original=Copy();original.Phase=4;Check(accepts(original),"durably terminal original supports post-write/archive current-proof reread");
        // Fresh terminal progression independently starts ONLY at2/3; phase4
        // acceptance here grants no additional publish, item or source action.
        if(actualSettledOriginal.Outcome==1&&actualSettledOriginal.Legs.Count>0)
        {original=Copy();original.Legs[0].Published=1;Check(!accepts(original),"missing final native source publication refuses");}
        if(actualSettledOriginal.Outcome==1&&actualSettledOriginal.Npcs.Count>0)
        {original=Copy();original.Npcs[0].Published=1;Check(!accepts(original),"missing final NPC source publication refuses");}
        // Authentication, saved3/4 Held1 and native fatal relation remain real
        // native scenario obligations. This pure predicate fixture grants none.
    }
}
