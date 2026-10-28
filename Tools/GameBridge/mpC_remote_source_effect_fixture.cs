using System;
using System.Linq;
// Deferred offline fixture. Compile together with the ACTUAL production
// RebirthRemoteResourceSourceEffectCodec.cs; no engine/native serializer double.
// This does not prove native publication, RPC authority or owner conservation.
internal static class RemoteSourceEffectChecks
{
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static string H(char value)=>new string(value,64);
    private static void Main()
    {
        var incarnation=Guid.NewGuid().ToString("N");var operation=Guid.NewGuid();
        var domain=RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource;
        var empty=RebirthRemoteResourceSourceEffectCodec.Empty(incarnation);
        Require(RebirthRemoteResourceSourceEffectCodec.TryRead(empty,incarnation,out var entries)&&entries.Length==0,"empty original ledger");
        Require(!RebirthRemoteResourceSourceEffectCodec.TryPlan(empty,incarnation,domain,operation,H('a'),H('b'),2,H('c'),out _),"credit cannot manufacture a debit receipt");
        Require(!RebirthRemoteResourceSourceEffectCodec.TryPlan(empty,incarnation,domain,operation,H('a'),H('b'),3,H('c'),out _),"restore cannot manufacture a debit receipt");
        Require(RebirthRemoteResourceSourceEffectCodec.TryPlan(empty,incarnation,domain,operation,H('a'),H('b'),1,H('c'),out var debit),"original debit");
        var retained=(byte[])debit.Clone();
        Require(RebirthRemoteResourceSourceEffectCodec.TryPlan(debit,incarnation,domain,operation,H('a'),H('b'),1,H('c'),out var replay)&&replay.SequenceEqual(debit),"exact debit replay");
        Require(!RebirthRemoteResourceSourceEffectCodec.TryPlan(debit,incarnation,domain,operation,H('d'),H('b'),1,H('c'),out _),"owner substitution");
        Require(!RebirthRemoteResourceSourceEffectCodec.TryPlan(debit,incarnation,domain,operation,H('a'),H('d'),1,H('c'),out _),"original binding substitution");
        Require(!RebirthRemoteResourceSourceEffectCodec.TryPlan(debit,incarnation,domain,operation,H('a'),H('b'),1,H('d'),out _),"same-stage delta substitution");
        Require(RebirthRemoteResourceSourceEffectCodec.TryPlan(debit,incarnation,domain,operation,H('a'),H('b'),2,H('d'),out var credit),"positive credit after debit");
        Require(!RebirthRemoteResourceSourceEffectCodec.TryPlan(credit,incarnation,domain,operation,H('a'),H('b'),3,H('e'),out _),"committed credit cannot become an unpaid restore");
        Require(!RebirthRemoteResourceSourceEffectCodec.TryPlan(credit,incarnation,domain,operation,H('a'),H('b'),1,H('c'),out _),"terminal stage cannot regress");
        Require(RebirthRemoteResourceSourceEffectCodec.TryPlan(debit,incarnation,domain,operation,H('a'),H('b'),3,H('e'),out var restored),"separate original restoration branch");
        Require(!RebirthRemoteResourceSourceEffectCodec.TryPlan(restored,incarnation,domain,operation,H('a'),H('b'),2,H('d'),out _),"restored source cannot later credit");
        Require(!RebirthRemoteResourceSourceEffectCodec.TryRead(debit,Guid.NewGuid().ToString("N"),out _),"replacement native incarnation");
        Require(!RebirthRemoteResourceSourceEffectCodec.TryRead(debit.Concat(new byte[]{0}).ToArray(),incarnation,out _),"trailing data");
        Require(!RebirthRemoteResourceSourceEffectCodec.TryRead(debit.Take(debit.Length-1).ToArray(),incarnation,out _),"truncated actual receipt");
        Require(debit.SequenceEqual(retained),"pure planner preserves retained input bytes");
        var full=empty;
        for(int index=0;index<RebirthRemoteResourceSourceEffectCodec.MaximumReceipts;index++)
        {Require(RebirthRemoteResourceSourceEffectCodec.TryPlan(full,incarnation,domain,Guid.NewGuid(),H('a'),H('b'),1,H('c'),out var next),"receipt capacity admission");full=next;}
        Require(!RebirthRemoteResourceSourceEffectCodec.TryPlan(full,incarnation,domain,Guid.NewGuid(),H('a'),H('b'),1,H('c'),out _),"capacity before effects");
        Require(RebirthRemoteResourceSourceEffectCodec.TryRetire(full,incarnation,effect=>false,out var unresolved)&&unresolved.SequenceEqual(full),"unknown originals never pruned");
        Require(RebirthRemoteResourceSourceEffectCodec.TryRetire(full,incarnation,effect=>effect.Operation==operation,out var unrelated)&&unrelated.SequenceEqual(full),"unrelated terminal cannot prune");
        Require(RebirthRemoteResourceSourceEffectCodec.TryRetire(credit,incarnation,effect=>effect.Operation==operation&&effect.Owner==H('a')&&effect.Binding==H('b')&&effect.Stage==2,out var retired)&&
            RebirthRemoteResourceSourceEffectCodec.TryRead(retired,incarnation,out entries)&&entries.Length==0,"exact positively terminal original retirement");
        Console.WriteLine("PASS: production bounded source-effect codec; native save/leases/transport/owner effects not exercised.");
    }
}
