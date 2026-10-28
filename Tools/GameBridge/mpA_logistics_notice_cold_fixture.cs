using System;
// PREPARED NOT RUN. Pure cold-candidate identity checks, no filesystem/effects.
internal static class mpA_logistics_notice_cold_fixture
{
    internal static void RequireCanonicalCandidates(Guid operation,Guid incarnation,Guid nonce,string digest)
    {
        if(operation==Guid.Empty||incarnation==Guid.Empty||nonce==Guid.Empty||digest==null||digest.Length!=64)throw new ArgumentException();
        string prefix=operation.ToString("N")+"_"+incarnation.ToString("N")+"_"+nonce.ToString("N")+"_";
        foreach(string suffix in new[]{".xml",".xml.tmp"})
        {
            string name=prefix+"1_"+digest+suffix;
            if(!RebirthLogisticsLoadedBodyNoticeJournal.TryColdIdentity(name,out var op,out var inc,out var n,out int life,out string d)||op!=operation||inc!=incarnation||n!=nonce||life!=1||d!=digest)throw new Exception("Canonical candidate changed identity.");
        }
        foreach(string name in new[]{prefix+"01_"+digest+".xml",prefix+"+1_"+digest+".xml",prefix+"0_"+digest+".xml",prefix+"1_"+digest+".xml.bak",prefix+"1_"+digest+".xml.tmp.tmp",prefix+"1_"+digest+"_extra.xml"})
            if(RebirthLogisticsLoadedBodyNoticeJournal.TryColdIdentity(name,out _,out _,out _,out _,out _))throw new Exception("Noncanonical candidate aliased an original path.");
    }
}
// Deferred actual IO: 4097 empty owners stops before body decode; root/owner
// reparse refuses; same primary+tmp decodes once through strict TryLoad; corrupt
// tmp refuses even when primary valid; unsupported backup stays unknown; one
// candidate cannot redirect to a canonical filename for a different life.
