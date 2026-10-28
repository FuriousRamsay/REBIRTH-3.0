using System;
using System.Reflection;

// Authored only. Calls the production predicate; no substitute host permission.
internal static class RemoteCraftRemainingCostChecks
{
    private static readonly MethodInfo Mixture=typeof(RebirthRemoteResourceOwnerAction).GetMethod("CraftMixture",BindingFlags.Static|BindingFlags.NonPublic);
    private static bool Accept(RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,RebirthCamperEnergyActorImage current)
        =>(bool)Mixture.Invoke(null,new object[]{before,after,current});
    internal static void Run(RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,
        RebirthCamperEnergyActorImage actualPartial,RebirthCamperEnergyActorImage unrelatedChanged,
        RebirthCamperEnergyActorImage cursorChanged,RebirthCamperEnergyActorImage locksChanged)
    {
        if(Mixture==null||!Accept(before,after,before)||!Accept(before,after,after)||!Accept(before,after,actualPartial))throw new Exception("original remaining-cost mixtures");
        if(Accept(before,after,unrelatedChanged)||Accept(before,after,cursorChanged)||Accept(before,after,locksChanged)||Accept(after,before,before))
            throw new Exception("foreign image or reverse credit must be refused");
    }
    // Genuine MP acceptance still requires an actual saved native1 accepted
    // card, canonical root2 ownerAction and durable host continuation receipt.
    // Interrupt after one permitted slot: reconnect must finish only remaining
    // slots, retain the same row/count/quality and then prove native2/fullAfter.
    // Duplicate/missing card, wrong life, native5, rejected root, changed cursor,
    // locks or unrelated slot must retain unknown without callback or refund.
}
